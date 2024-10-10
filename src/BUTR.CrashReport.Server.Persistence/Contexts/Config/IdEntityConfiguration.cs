using BUTR.CrashReport.Server.Models.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BUTR.CrashReport.Server.Contexts.Config;

public class IdEntityConfiguration : BaseEntityConfiguration<IdEntity>
{
    protected override void ConfigureModel(EntityTypeBuilder<IdEntity> builder)
    {
        builder.Property(x => x.CrashReportId).HasColumnName("crash_report_id");
        builder.Property(x => x.FileId).HasColumnName("file_id");
        builder.ToTable("id_entity").HasKey(x => x.CrashReportId).HasName("id_entity_pkey");

        builder.HasIndex(x => x.FileId).IsUnique(false).HasDatabaseName("id_entity_file_id_idx");
    }
}