using BUTR.CrashReport.Server.Models.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BUTR.CrashReport.Server.Contexts.Config;

public class IdAliasEntityConfiguration : BaseEntityConfiguration<IdAliasEntity>
{
    protected override void ConfigureModel(EntityTypeBuilder<IdAliasEntity> builder)
    {
        builder.Property(x => x.FileId).HasColumnName("file_id");
        builder.Property(x => x.CrashReportId).HasColumnName("crash_report_id");
        builder.Property(x => x.Tenant).HasColumnName("tenant");
        builder.ToTable("id_alias_entity").HasKey(x => x.FileId).HasName("id_alias_entity_pkey");

        // Same per-tenant uniqueness as the canonical report_entity.file_id, so a file id resolves to one report.
        builder.HasIndex(x => new { x.FileId, x.Tenant }).IsUnique().HasDatabaseName("id_alias_entity_file_id_tenant_idx");
    }
}