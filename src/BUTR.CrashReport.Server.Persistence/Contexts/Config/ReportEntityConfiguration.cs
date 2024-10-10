using BUTR.CrashReport.Server.Models.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BUTR.CrashReport.Server.Contexts.Config;

public class ReportEntityConfiguration : BaseEntityConfiguration<ReportEntity>
{
    protected override void ConfigureModel(EntityTypeBuilder<ReportEntity> builder)
    {
        builder.Property(x => x.CrashReportId).HasColumnName("crash_report_id");
        builder.Property(x => x.Tenant).HasColumnName("tenant");
        builder.Property(x => x.Version).HasColumnName("version");
        builder.Property(x => x.Created).HasColumnName("created");
        builder.ToTable("report_entity").HasKey(x => x.CrashReportId).HasName("report_entity_pkey");

        builder.HasOne(x => x.Id)
            .WithOne(x => x.Report)
            .HasForeignKey<IdEntity>(x => x.CrashReportId)
            .HasPrincipalKey<ReportEntity>(x => x.CrashReportId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("report_entity_id_entity_fkey");

        builder.HasOne(x => x.Html)
            .WithOne(x => x.Report)
            .HasForeignKey<HtmlEntity>(x => x.CrashReportId)
            .HasPrincipalKey<ReportEntity>(x => x.CrashReportId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("report_entity_html_entity_fkey");

        builder.HasOne(x => x.Json)
            .WithOne(x => x.Report)
            .HasForeignKey<JsonEntity>(x => x.CrashReportId)
            .HasPrincipalKey<ReportEntity>(x => x.CrashReportId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("report_entity_json_entity_fkey");
    }
}