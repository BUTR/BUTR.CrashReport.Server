using BUTR.CrashReport.Server.Models.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BUTR.CrashReport.Server.Contexts.Config;

public class JsonEntityConfiguration : BaseEntityConfiguration<JsonEntity>
{
    protected override void ConfigureModel(EntityTypeBuilder<JsonEntity> builder)
    {
        builder.Property(x => x.CrashReportId).HasColumnName("crash_report_id");
        builder.Property(x => x.DataCompressed).HasColumnName("data_compressed");
        builder.Property(x => x.DictId).HasColumnName("dict_id");
        builder.ToTable("json_entity").HasKey(x => x.CrashReportId);

        builder.HasIndex(x => x.CrashReportId)
            .HasDatabaseName("ix_json_backfill")
            .HasFilter("dict_id IS NULL");

        builder.HasOne(x => x.Dictionary)
            .WithMany()
            .HasForeignKey(x => x.DictId)
            .HasPrincipalKey(x => x.Id)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("json_entity_compression_dictionary_fkey");
    }
}