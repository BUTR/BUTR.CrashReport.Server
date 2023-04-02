using BUTR.CrashReportServer.Models.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BUTR.CrashReportServer.Contexts.Config
{
    public abstract class BaseEntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity> where TEntity : class, IEntity
    {
        public void Configure(EntityTypeBuilder<TEntity> entity)
        {
            ConfigureModel(entity);
        }

        protected abstract void ConfigureModel(EntityTypeBuilder<TEntity> entity);
    }
}