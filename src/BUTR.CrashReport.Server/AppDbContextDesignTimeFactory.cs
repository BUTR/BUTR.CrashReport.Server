using BUTR.CrashReport.Server.Contexts;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BUTR.CrashReport.Server;

/// <summary>
/// Design-time factory used only by the EF Core tools (migrations). Builds the context directly so the tools don't
/// have to spin up the application host (which fails to load the ILRepack-merged versioned assemblies). The
/// connection string is a placeholder - "migrations add" does not connect to a database.
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=design;Username=design;Password=design",
                x => x.MigrationsAssembly("BUTR.CrashReport.Server"))
            .Options;
        return new AppDbContext(options);
    }
}
