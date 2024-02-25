using BUTR.CrashReportServer.Contexts.Config;
using BUTR.CrashReportServer.Models.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BUTR.CrashReportServer.Contexts;

public class OldFileEntityConfiguration : BaseEntityConfiguration<OldFileEntity>
{
    protected override void ConfigureModel(EntityTypeBuilder<OldFileEntity> builder)
    {
        builder.Property<string>(nameof(IdEntity.FileId)).HasColumnName("file_id");
        builder.Property(p => p.DataCompressed).HasColumnName("data_compressed");
        builder.ToTable("file_entity").HasKey(x => x.RowId);
        builder.HasAlternateKey(nameof(IdEntity.FileId)).HasName("file_entity_pkey");

        builder.HasOne(x => x.Id)
            .WithOne()
            .HasForeignKey<OldFileEntity>(nameof(IdEntity.FileId))
            .HasPrincipalKey<OldIdEntity>(x => x.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Id).AutoInclude();
    }
}
public class OldIdEntityConfiguration : BaseEntityConfiguration<OldIdEntity>
{
    protected override void ConfigureModel(EntityTypeBuilder<OldIdEntity> builder)
    {
        builder.Property(x => x.FileId).HasColumnName("file_id");
        builder.Property(x => x.CrashReportId).HasColumnName("crash_report_id");
        builder.Property(x => x.Version).HasColumnName("version");
        builder.Property(x => x.Created).HasColumnName("created");
        builder.ToTable("id_entity").HasKey(x => x.RowId);
        builder.HasAlternateKey(x => x.FileId);

        builder.HasIndex(x => x.CrashReportId).IsUnique(false);
    }
}
public class OldJsonEntityConfiguration : BaseEntityConfiguration<OldJsonEntity>
{
    protected override void ConfigureModel(EntityTypeBuilder<OldJsonEntity> builder)
    {
        builder.Property<string>(nameof(IdEntity.FileId)).HasColumnName("file_id");
        builder.Property(p => p.CrashReportCompressed).HasColumnName("data_compressed");
        builder.ToTable("json_entity").HasKey(x => x.RowId);
        builder.HasAlternateKey(nameof(IdEntity.FileId));

        builder.HasOne(x => x.Id)
            .WithOne()
            .HasForeignKey<OldJsonEntity>(nameof(IdEntity.FileId))
            .HasPrincipalKey<OldIdEntity>(x => x.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Id).AutoInclude();
    }
}

public class OldAppDbContext : DbContext
{
    public DbSet<OldIdEntity> IdEntities { get; set; }
    public DbSet<OldFileEntity> FileEntities { get; set; }
    public DbSet<OldJsonEntity> JsonEntities { get; set; }
    
    public OldAppDbContext(DbContextOptions<OldAppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new OldIdEntityConfiguration());
        modelBuilder.ApplyConfiguration(new OldFileEntityConfiguration());
        modelBuilder.ApplyConfiguration(new OldJsonEntityConfiguration());
    }
}