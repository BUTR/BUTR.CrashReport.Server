using BUTR.CrashReport.Server.Models.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BUTR.CrashReport.Server.Contexts.Config;

public class IdEntityConfiguration : BaseEntityConfiguration<IdEntity>
{
    protected override void ConfigureModel(EntityTypeBuilder<IdEntity> builder)
    {
        builder.Property(x => x.FileId).HasColumnName("file_id");
        builder.Property(x => x.CrashReportId).HasColumnName("crash_report_id");
        builder.Property(x => x.Version).HasColumnName("version");
        builder.Property(x => x.Created).HasColumnName("created");
        builder.ToTable("id_entity").HasKey(x => x.FileId);

        builder.HasIndex(x => x.CrashReportId).IsUnique(false);
    }
}