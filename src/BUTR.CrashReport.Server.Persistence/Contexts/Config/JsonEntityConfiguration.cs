using BUTR.CrashReport.Server.Models.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BUTR.CrashReport.Server.Contexts.Config;

public class JsonEntityConfiguration : BaseEntityConfiguration<JsonEntity>
{
    protected override void ConfigureModel(EntityTypeBuilder<JsonEntity> builder)
    {
        builder.Property(x => x.CrashReportId).HasColumnName("crash_report_id");
        builder.Property(x => x.Json).HasColumnName("data").HasColumnType("jsonb");
        builder.ToTable("json_entity").HasKey(x => x.CrashReportId);
    }
}