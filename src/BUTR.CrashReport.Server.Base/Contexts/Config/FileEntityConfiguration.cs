using BUTR.CrashReport.Server.Models.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BUTR.CrashReport.Server.Contexts.Config;

public class FileEntityConfiguration : BaseEntityConfiguration<FileEntity>
{
    protected override void ConfigureModel(EntityTypeBuilder<FileEntity> builder)
    {
        builder.Property(x => x.FileId).HasColumnName("file_id");
        builder.Property(x => x.DataCompressed).HasColumnName("data_compressed");
        builder.ToTable("file_entity").HasKey(x => x.FileId).HasName("file_entity_pkey");

        builder.HasOne(x => x.Id)
            .WithOne()
            .HasForeignKey<FileEntity>(x => x.FileId)
            .HasPrincipalKey<IdEntity>(x => x.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Id).AutoInclude();
    }
}