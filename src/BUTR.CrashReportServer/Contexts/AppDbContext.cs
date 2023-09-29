using BUTR.CrashReportServer.Contexts.Config;

using Microsoft.EntityFrameworkCore;

namespace BUTR.CrashReportServer.Contexts;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new IdEntityConfiguration());
        modelBuilder.ApplyConfiguration(new FileEntityConfiguration());
        modelBuilder.ApplyConfiguration(new JsonEntityConfiguration());
    }
}