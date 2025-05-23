﻿using AspNetCore.Authentication.Basic;

using BUTR.CrashReport.Server.Contexts;
using BUTR.CrashReport.Server.Options;
using BUTR.CrashReport.Server.Services;
using BUTR.CrashReport.Server.v13;
using BUTR.CrashReport.Server.v14;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IO;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BUTR.CrashReport.Server;

public class Startup
{
    private readonly string _appName;
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _appName = Assembly.GetEntryAssembly()?.GetName().Name ?? "ERROR";
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public void ConfigureServices(IServiceCollection services)
    {
        var assemblyName = typeof(Startup).Assembly.GetName();
        var userAgent = $"{assemblyName.Name ?? "ERROR"} v{assemblyName.Version?.ToString() ?? "ERROR"} (github.com/BUTR)";

        services.Configure<AuthOptions>(_configuration.GetSection("Auth"));
        services.Configure<StorageOptions>(_configuration.GetSection("Storage"));
        services.Configure<CrashUploadOptions>(_configuration.GetSection("CrashUpload"));
        services.Configure<ReportOptions>(_configuration.GetSection("Report"));
        services.Configure<UptimeKumaOptions>(_configuration.GetSection("UptimeKuma"));

        services.AddHttpClient<IHealthCheckPublisher, UptimeKumaHealthCheckPublisher>().ConfigureHttpClient((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<UptimeKumaOptions>>().Value;

            if (Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var uri))
                client.BaseAddress = uri;
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        });

        services.AddTransient<RandomNumberGenerator>(_ => RandomNumberGenerator.Create());
        services.AddScoped<FileIdGenerator>();
        services.AddScoped<HtmlHandlerV13>();
        services.AddScoped<JsonHandlerV13>();
        services.AddScoped<HtmlHandlerV14>();
        services.AddScoped<JsonHandlerV14>();
        services.AddSingleton<HexGenerator>();
        services.AddSingleton<RecyclableMemoryStreamManager>();
        services.AddSingleton<GZipCompressor>();
        //services.AddHostedService<DatabaseMigrator>();

        services.AddDbContextFactory<AppDbContext>(x => x.UseNpgsql(_configuration.GetConnectionString("Main"), y => y.MigrationsAssembly("BUTR.CrashReport.Server")));

        services.AddSwaggerGen(opt =>
        {
            opt.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1",
                Title = "BUTR's Crash Report Server",
                Description = "BUTR's service used for ingesting crash reports",
            });

            var basicSecurityScheme = new OpenApiSecurityScheme
            {
                Scheme = BasicDefaults.AuthenticationScheme,
                Name = HeaderNames.Authorization,
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Description = "Basic Authorization header using the Bearer scheme.",

                Reference = new OpenApiReference
                {
                    Id = BasicDefaults.AuthenticationScheme,
                    Type = ReferenceType.SecurityScheme
                }
            };
            opt.AddSecurityDefinition(basicSecurityScheme.Reference.Id, basicSecurityScheme);
            opt.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { basicSecurityScheme, Array.Empty<string>() }
            });

            opt.DescribeAllParametersInCamelCase();
            opt.SupportNonNullableReferenceTypes();

            var currentAssembly = typeof(Startup).Assembly;
            var xmlFilePaths = currentAssembly.GetReferencedAssemblies()
                .Append(currentAssembly.GetName())
                .Select(x => Path.Combine(Path.GetDirectoryName(currentAssembly.Location)!, $"{x.Name}.xml"))
                .Where(File.Exists)
                .ToList();
            foreach (var xmlFilePath in xmlFilePaths)
                opt.IncludeXmlComments(xmlFilePath);
        });

        services.AddAuthentication(BasicDefaults.AuthenticationScheme).AddBasic<BasicUserValidationService>(options =>
        {
            options.Realm = "BUTR.CrashReportServer";
            options.IgnoreAuthenticationIfAllowAnonymous = true;
        });

        services.AddControllers().AddJsonOptions(opts =>
        {
            opts.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }).AddXmlSerializerFormatters().AddXmlDataContractSerializerFormatters();
        services.Configure<JsonSerializerOptions>(opts =>
        {
            opts.PropertyNameCaseInsensitive = true;
            opts.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            opts.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        });

        /*
        services.AddResponseCompression(opts =>
        {
            opts.EnableForHttps = true;
            opts.Providers.Add<BrotliCompressionProvider>();
            opts.Providers.Add<GzipCompressionProvider>();
        });
        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest;
        });

        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.SmallestSize;
        });
        */

        services.AddResponseCaching();

        services.AddHealthChecks()
            .AddNpgSql(_configuration.GetConnectionString("Main")!);
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseSwagger();
        app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", _appName));

        app.UseHealthChecks("/healthz");

        app.UseRouting();

        app.UseResponseCaching();
        //app.UseResponseCompression();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}