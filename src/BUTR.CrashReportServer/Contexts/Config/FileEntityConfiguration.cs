﻿using BUTR.CrashReportServer.Models.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BUTR.CrashReportServer.Contexts.Config;

public class FileEntityConfiguration : BaseEntityConfiguration<FileEntity>
{
    protected override void ConfigureModel(EntityTypeBuilder<FileEntity> builder)
    {
        builder.Property<string>(nameof(IdEntity.FileId)).HasColumnName("file_id");
        builder.Property(p => p.DataCompressed).HasColumnName("data_compressed");
        builder.ToTable("file_entity").HasKey(nameof(IdEntity.FileId)).HasName("file_entity_pkey");

        builder.HasOne(x => x.Id)
            .WithOne()
            .HasForeignKey<FileEntity>(nameof(IdEntity.FileId))
            .HasPrincipalKey<IdEntity>(x => x.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Id).AutoInclude();
    }
}