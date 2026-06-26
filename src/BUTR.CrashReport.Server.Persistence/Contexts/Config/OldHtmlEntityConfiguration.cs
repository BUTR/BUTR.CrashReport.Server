using BUTR.CrashReport.Server.Models.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BUTR.CrashReport.Server.Contexts.Config;

public class OldHtmlEntityConfiguration : BaseEntityConfiguration<OldHtmlEntity>
{
    protected override void ConfigureModel(EntityTypeBuilder<OldHtmlEntity> builder)
    {
        builder.Property(x => x.CrashReportId).HasColumnName("crash_report_id");
        builder.Property(x => x.DataCompressed).HasColumnName("data_compressed");
        builder.ToTable("old_html_entity").HasKey(x => x.CrashReportId).HasName("old_html_entity_pkey");
    }
}
