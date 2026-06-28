using BUTR.CrashReport.Server.Models.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BUTR.CrashReport.Server.Contexts.Config;

public class CompressionDictionaryEntityConfiguration : BaseEntityConfiguration<CompressionDictionaryEntity>
{
    protected override void ConfigureModel(EntityTypeBuilder<CompressionDictionaryEntity> builder)
    {
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.Tenant).HasColumnName("tenant");
        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<byte>();
        builder.Property(x => x.Version).HasColumnName("version");
        builder.Property(x => x.Bytes).HasColumnName("bytes");
        builder.Property(x => x.Created).HasColumnName("created");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.ToTable("compression_dictionary").HasKey(x => x.Id).HasName("compression_dictionary_pkey");

        builder.HasIndex(x => new { x.Tenant, x.Kind, x.Version })
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName("compression_dictionary_active_idx");
    }
}