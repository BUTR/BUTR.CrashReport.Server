using BUTR.CrashReport.Server.Models.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BUTR.CrashReport.Server.Contexts.Config;

public class JsonEntityConfiguration : BaseEntityConfiguration<JsonEntity>
{
    protected override void ConfigureModel(EntityTypeBuilder<JsonEntity> builder)
    {
        builder.Property(x => x.FileId).HasColumnName("file_id");
        builder.Property(x => x.CrashReport).HasColumnName("data").HasColumnType("jsonb");
        builder.ToTable("json_entity").HasKey(x => x.FileId);

        builder.HasOne(x => x.Id)
            .WithOne()
            .HasForeignKey<JsonEntity>(x => x.FileId)
            .HasPrincipalKey<IdEntity>(x => x.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Id).AutoInclude();
    }
}