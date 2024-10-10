using BUTR.CrashReport.Server.Contexts.Config;
using BUTR.CrashReport.Server.Models.Database;

using Microsoft.EntityFrameworkCore;

namespace BUTR.CrashReport.Server.Contexts;

public class AppDbContext : DbContext
{
    public DbSet<ReportEntity> ReportEntities { get; set; }
    public DbSet<IdEntity> IdEntities { get; set; }
    public DbSet<HtmlEntity> HtmlEntities { get; set; }
    public DbSet<JsonEntity> JsonEntities { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new ReportEntityConfiguration());
        modelBuilder.ApplyConfiguration(new IdEntityConfiguration());
        modelBuilder.ApplyConfiguration(new HtmlEntityConfiguration());
        modelBuilder.ApplyConfiguration(new JsonEntityConfiguration());
    }
}