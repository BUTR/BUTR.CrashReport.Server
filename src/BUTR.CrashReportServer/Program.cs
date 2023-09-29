using BUTR.CrashReportServer.Contexts;
using BUTR.CrashReportServer.Extensions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

using System.Security.Authentication;
using System.Threading.Tasks;

namespace BUTR.CrashReportServer;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = CreateHostBuilder(args);
        var host = builder.Build();
        await host.SeedDbContextAsync<AppDbContext>();
        await host.RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) => Host
        .CreateDefaultBuilder(args)
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
        });
}