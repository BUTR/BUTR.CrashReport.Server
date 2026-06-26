using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.Linq;
using System.Threading.Tasks;

namespace BUTR.CrashReport.Server.Extensions;

public static class HostExtensions
{
    private static readonly TimeSpan MigrationCommandTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Applies all pending migrations. Intended to run as an explicit one-shot deploy step (before the app serves),
    /// not as a side effect of booting the web host. Returns a process exit code (0 success, 1 failure).
    /// </summary>
    public static async Task<int> MigrateDatabaseAsync<TDbContext>(this IHost host) where TDbContext : DbContext
    {
        await using var scope = host.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TDbContext>>();
        try
        {
            dbContext.Database.SetCommandTimeout(MigrationCommandTimeout);
            var pending = (await dbContext.Database.GetPendingMigrationsAsync()).ToArray();
            if (pending.Length == 0)
            {
                logger.LogInformation("Database is up to date; no migrations to apply");
                return 0;
            }

            logger.LogInformation("Applying {Count} migration(s): {Migrations}", pending.Length, string.Join(", ", pending));
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Migrations applied successfully");
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply migrations");
            return 1;
        }
    }

    /// <summary>
    /// Reverts the database to <paramref name="targetMigrationId"/> by running the Down() of every migration applied
    /// after it. Use "0" to revert everything. Must run with an image that contains the migrations being reverted
    /// (i.e. the new image during a rollback), since only it defines their Down(). Returns a process exit code.
    /// </summary>
    public static async Task<int> MigrateDatabaseDownAsync<TDbContext>(this IHost host, string targetMigrationId) where TDbContext : DbContext
    {
        await using var scope = host.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TDbContext>>();
        try
        {
            dbContext.Database.SetCommandTimeout(MigrationCommandTimeout);
            logger.LogWarning("Reverting database down to migration {Target}", targetMigrationId);
            var migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(targetMigrationId);
            logger.LogInformation("Database reverted to {Target}", targetMigrationId);
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to revert database to {Target}", targetMigrationId);
            return 1;
        }
    }

    /// <summary>
    /// Prefix that uniquely tags the machine-readable migration id line on stdout. The deploy greps for this so
    /// interleaved logging (Serilog also writes to the console) can never be mistaken for the id.
    /// </summary>
    public const string CurrentMigrationMarker = "::MIGRATION_ID::";

    /// <summary>
    /// Prints the last-applied migration id to stdout (or "0" if none), prefixed with <see cref="CurrentMigrationMarker"/>.
    /// The deploy captures this before migrating so it has a target to revert to. Returns a process exit code.
    /// </summary>
    public static async Task<int> PrintCurrentMigrationAsync<TDbContext>(this IHost host) where TDbContext : DbContext
    {
        await using var scope = host.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TDbContext>>();
        try
        {
            dbContext.Database.SetCommandTimeout(MigrationCommandTimeout);
            var applied = (await dbContext.Database.GetAppliedMigrationsAsync()).ToArray();
            // Marker-prefixed so the deploy can extract it precisely; logging may interleave on the same stream.
            Console.WriteLine($"{CurrentMigrationMarker}{(applied.Length == 0 ? "0" : applied[^1])}");
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read applied migrations");
            return 1;
        }
    }

    /// <summary>
    /// Throws if the database has pending migrations. The serve path calls this so the app can never start against a
    /// schema it doesn't match - migrations are an explicit deploy step now, not a boot-time side effect.
    /// </summary>
    public static async Task EnsureDatabaseUpToDateAsync<TDbContext>(this IHost host) where TDbContext : DbContext
    {
        await using var scope = host.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TDbContext>>();

        dbContext.Database.SetCommandTimeout(MigrationCommandTimeout);
        var pending = (await dbContext.Database.GetPendingMigrationsAsync()).ToArray();
        if (pending.Length == 0) return;

        logger.LogCritical("Refusing to start: {Count} pending migration(s): {Migrations}. Run the 'migrate' step first.",
            pending.Length, string.Join(", ", pending));
        throw new InvalidOperationException($"Database has {pending.Length} pending migration(s); run the 'migrate' deploy step before serving.");
    }
}
 }

        return host;
    }
}