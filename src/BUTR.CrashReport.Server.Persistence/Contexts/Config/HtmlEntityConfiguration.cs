using BUTR.CrashReport.Server.Models.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BUTR.CrashReport.Server.Contexts.Config;

public class HtmlEntityConfiguration : BaseEntityConfiguration<HtmlEntity>
{
    protected override void ConfigureModel(EntityTypeBuilder<HtmlEntity> builder)
    {
        builder.Property(x => x.CrashReportId).HasColumnName("crash_report_id");
        builder.Property(x => x.DataCompressed).HasColumnName("data_compressed");
        builder.ToTable("html_entity").HasKey(x => x.CrashReportId).HasName("html_entity_pkey");

        builder.HasOne(x => x.Id)
            .WithOne()
            .HasForeignKey<IdEntity>(x => x.CrashReportId)
            .HasPrincipalKey<HtmlEntity>(x => x.CrashReportId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("html_entity_id_entity_fkey");
    }
}