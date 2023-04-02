using BUTR.CrashReportServer.Models.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BUTR.CrashReportServer.Contexts.Config
{
    public class FileEntityConfiguration : BaseEntityConfiguration<FileEntity>
    {
        protected override void ConfigureModel(EntityTypeBuilder<FileEntity> builder)
        {
            builder.ToTable("file_entity").HasKey(p => p.Name).HasName("file_entity_pkey");
            builder.Property(p => p.Name).HasColumnName("name").IsRequired();
            builder.Property(p => p.Created).HasColumnName("created").IsRequired();
            builder.Property(p => p.Modified).HasColumnName("modified").IsRequired();
            builder.Property(p => p.SizeOriginal).HasColumnName("size_original").IsRequired();
            builder.Property(p => p.DataCompressed).HasColumnName("data_compressed").IsRequired();
        }
    }
}