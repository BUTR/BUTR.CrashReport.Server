using BUTR.CrashReportServer.Models.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BUTR.CrashReportServer.Contexts.Config;

public class JsonEntityConfiguration : BaseEntityConfiguration<JsonEntity>
{
    protected override void ConfigureModel(EntityTypeBuilder<JsonEntity> builder)
    {
        builder.Property<string>(nameof(IdEntity.FileId)).HasColumnName("file_id");
        builder.Property(p => p.CrashReport).HasColumnName("data").HasColumnType("jsonb");
        builder.ToTable("json_entity").HasKey(nameof(IdEntity.FileId));

        builder.HasOne(x => x.Id)
            .WithOne()
            .HasForeignKey<JsonEntity>(nameof(IdEntity.FileId))
            .HasPrincipalKey<IdEntity>(x => x.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Id).AutoInclude();
    }
}