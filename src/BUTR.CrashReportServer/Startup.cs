using AspNetCore.Authentication.Basic;

using BUTR.CrashReportServer.Options;
using BUTR.CrashReportServer.Services;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BUTR.CrashReportServer
{
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
            services.Configure<AuthOptions>(_configuration.GetSection("Auth"));
            services.Configure<StorageOptions>(_configuration.GetSection("Storage"));
            services.Configure<CrashUploadOptions>(_configuration.GetSection("CrashUpload"));

            services.AddSingleton<IFilePathProvider, SemaphoreFilePathProvider>();

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

                var currentAssembly = Assembly.GetExecutingAssembly();
                var xmlFilePaths = currentAssembly.GetReferencedAssemblies()
                    .Append(currentAssembly.GetName())
                    .Select(x => Path.Combine(Path.GetDirectoryName(currentAssembly.Location)!, $"{x.Name}.xml"))
                    .Where(File.Exists)
                    .ToList();
                foreach (var xmlFilePath in xmlFilePaths)
                    opt.IncludeXmlComments(xmlFilePath);
            });

            services.AddAuthentication(BasicDefaults.AuthenticationScheme)
                .AddBasic<BasicUserValidationService>(options =>
                {
                    options.Realm = "BUTR.CrashReportServer";
                    options.IgnoreAuthenticationIfAllowAnonymous = true;
                });

            services.AddControllers().AddJsonOptions(opts =>
            {
                opts.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSwagger();
            app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", _appName));

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}