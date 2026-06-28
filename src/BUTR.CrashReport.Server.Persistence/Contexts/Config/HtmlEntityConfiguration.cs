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
        builder.Property(x => x.DictId).HasColumnName("dict_id");
        builder.ToTable("html_entity").HasKey(x => x.CrashReportId).HasName("html_entity_pkey");

        builder.HasOne(x => x.Dictionary)
            .WithMany()
            .HasForeignKey(x => x.DictId)
            .HasPrincipalKey(x => x.Id)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("html_entity_compression_dictionary_fkey");
    }
}