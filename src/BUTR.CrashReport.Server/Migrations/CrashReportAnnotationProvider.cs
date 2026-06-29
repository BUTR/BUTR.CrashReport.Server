using BUTR.CrashReport.Server.Contexts;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.Internal;

using System.Collections.Generic;

namespace BUTR.CrashReport.Server.Migrations;

/// <summary>
/// Surfaces the <see cref="CrashReportInsertFunction.AnnotationName"/> model annotation onto the relational model so
/// the migrations differ sees it and emits an <see cref="Microsoft.EntityFrameworkCore.Migrations.Operations.AlterDatabaseOperation"/>
/// whenever it changes. <see cref="CrashReportMigrationsSqlGenerator"/> then turns that into CREATE OR REPLACE / DROP.
/// </summary>
public sealed class CrashReportAnnotationProvider : NpgsqlAnnotationProvider
{
    public CrashReportAnnotationProvider(RelationalAnnotationProviderDependencies dependencies) : base(dependencies) { }

    public override IEnumerable<IAnnotation> For(IRelationalModel model, bool designTime)
    {
        foreach (var annotation in base.For(model, designTime))
            yield return annotation;

        // When enabled (and the model has the required schema - older snapshots may not), generate the function SQL
        // from the fully-resolved relational model (names + store types).
        if (model.Model.FindAnnotation(CrashReportInsertFunction.MarkerAnnotationName) is not null && CrashReportInsertFunction.CanBuild(model))
            yield return new Annotation(CrashReportInsertFunction.AnnotationName, CrashReportInsertFunction.BuildCreateSql(model));
    }
}