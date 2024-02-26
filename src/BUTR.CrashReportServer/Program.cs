﻿using BUTR.CrashReportServer.Contexts;
using BUTR.CrashReportServer.Extensions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.ResourceDetectors.Container;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Serilog;
using Serilog.Events;

using System;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace BUTR.CrashReportServer;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting web application");

            var builder = CreateHostBuilder(args);
            var host = builder.Build();
            await host.SeedDbContextAsync<AppDbContext>();
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) => Host
        .CreateDefaultBuilder(args)
        .ConfigureServices((ctx, services) =>
        {
            services.Configure<HostOptions>(hostOptions =>
            {
                hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
            });
            if (ctx.Configuration.GetSection("Oltp") is { } oltpSection)
            {
                var openTelemetry = services.AddOpenTelemetry()
                    .ConfigureResource(builder =>
                    {
                        builder.AddDetector(new ContainerResourceDetector());
                        builder.AddService(
                            ctx.HostingEnvironment.ApplicationName,
                            ctx.HostingEnvironment.EnvironmentName,
                            typeof(Program).Assembly.GetName().Version?.ToString(),
                            false,
                            Environment.MachineName);
                        builder.AddTelemetrySdk();
                    });

                if (oltpSection.GetValue<string?>("MetricsEndpoint") is { } metricsEndpoint)
                {
                    var metricsProtocol = oltpSection.GetValue<OtlpExportProtocol>("MetricsProtocol");
                    openTelemetry.WithMetrics(builder => builder
                        .AddMeter("BUTR.CrashReportServer.Controllers.CrashUploadController")
                        .AddProcessInstrumentation()
                        .AddRuntimeInstrumentation(instrumentationOptions =>
                        {

                        })
                        .AddAspNetCoreInstrumentation()
                        .AddOtlpExporter(o =>
                        {
                            o.Endpoint = new Uri(metricsEndpoint);
                            o.Protocol = metricsProtocol;
                        }));
                }

                if (oltpSection.GetValue<string?>("TracingEndpoint") is { } tracingEndpoint)
                {
                    var tracingProtocol = oltpSection.GetValue<OtlpExportProtocol>("TracingProtocol");
                    openTelemetry.WithTracing(builder => builder
                        .AddEntityFrameworkCoreInstrumentation(instrumentationOptions =>
                        {
                            instrumentationOptions.SetDbStatementForText = true;
                        })
                        .AddAspNetCoreInstrumentation(instrumentationOptions =>
                        {
                            instrumentationOptions.RecordException = true;
                        })
                        .AddOtlpExporter(o =>
                        {
                            o.Endpoint = new Uri(tracingEndpoint);
                            o.Protocol = tracingProtocol;
                        }));
                }
            }
        })
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseKestrel(kestrelOptions =>
            {
                kestrelOptions.ConfigureHttpsDefaults(httpsOptions =>
                {
                    httpsOptions.SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;
                });
            });

            webBuilder.UseStartup<Startup>();
        })
        .UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services);
        }, writeToProviders: true)
        .ConfigureLogging((ctx, builder) =>
        {
            var oltpSection = ctx.Configuration.GetSection("Oltp");
            if (oltpSection == null!) return;

            var loggingEndpoint = oltpSection.GetValue<string>("LoggingEndpoint");
            if (loggingEndpoint is null) return;
            var loggingProtocol = oltpSection.GetValue<OtlpExportProtocol>("LoggingProtocol");

            builder.AddOpenTelemetry(o =>
            {
                o.IncludeScopes = true;
                o.ParseStateValues = true;
                o.IncludeFormattedMessage = true;
                o.AddOtlpExporter((options, processorOptions) =>
                {
                    options.Endpoint = new Uri(loggingEndpoint);
                    options.Protocol = loggingProtocol;
                });
            });
        });
}