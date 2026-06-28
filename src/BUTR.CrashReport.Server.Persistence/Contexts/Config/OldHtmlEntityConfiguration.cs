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
        builder.Property(x => x.DictId).HasColumnName("dict_id");
        builder.ToTable("old_html_entity").HasKey(x => x.CrashReportId).HasName("old_html_entity_pkey");

        builder.HasOne(x => x.Dictionary)
            .WithMany()
            .HasForeignKey(x => x.DictId)
            .HasPrincipalKey(x => x.Id)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("old_html_entity_compression_dictionary_fkey");
    }
}