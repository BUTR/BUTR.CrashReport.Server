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
        builder.Property(x => x.FileId).HasColumnName("file_id");
        builder.Property(x => x.DeleteTokenHash).HasColumnName("delete_token_hash");
        builder.ToTable("report_entity").HasKey(x => x.CrashReportId).HasName("report_entity_pkey");

        builder.HasIndex(x => new { x.FileId, x.Tenant }).IsUnique().HasDatabaseName("report_entity_file_id_tenant_idx");

        builder.HasMany(x => x.Aliases)
            .WithOne(x => x.Report)
            .HasForeignKey(x => x.CrashReportId)
            .HasPrincipalKey(x => x.CrashReportId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("report_entity_id_alias_entity_fkey");

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

        builder.HasOne(x => x.OldHtml)
            .WithOne(x => x.Report)
            .HasForeignKey<OldHtmlEntity>(x => x.CrashReportId)
            .HasPrincipalKey<ReportEntity>(x => x.CrashReportId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("report_entity_old_html_entity_fkey");
    }
}