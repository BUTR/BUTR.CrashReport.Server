using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.Linq;
using System.Threading.Tasks;

namespace BUTR.CrashReportServer.Extensions;

public static class HostExtensions
{
    public static async Task<IHost> SeedDbContextAsync<TDbContext>(this IHost host) where TDbContext : DbContext
    {
        await using var scope = host.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TDbContext>>();
        try
        {
            var migrations = (await dbContext.Database.GetPendingMigrationsAsync()).Count();
            await dbContext.Database.MigrateAsync();
            //if (migrations > 0) await dbContext.Database.ExecuteSqlRawAsync("VACUUM;");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed the database");
            throw;
        }

        return host;
    }
}