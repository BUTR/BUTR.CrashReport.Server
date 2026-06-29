using BUTR.CrashReport.Server.Contexts;

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;

namespace BUTR.CrashReport.Server.Migrations;

/// <summary>
/// Emits CREATE OR REPLACE / DROP for the insert_crash_report function whenever its model annotation changes.
/// The annotation is a model-level value (see <see cref="CrashReportInsertFunction"/>), so the migrations differ
/// surfaces a change to it as an <see cref="AlterDatabaseOperation"/> that this generator translates into SQL.
/// </summary>
public sealed class CrashReportMigrationsSqlGenerator : NpgsqlMigrationsSqlGenerator
{
    public CrashReportMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, INpgsqlSingletonOptions npgsqlSingletonOptions)
        : base(dependencies, npgsqlSingletonOptions) { }

    protected override void Generate(AlterDatabaseOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        base.Generate(operation, model, builder);

        var newSql = operation[CrashReportInsertFunction.AnnotationName] as string;
        var oldSql = operation.OldDatabase[CrashReportInsertFunction.AnnotationName] as string;

        if (newSql is not null && newSql != oldSql)
        {
            // Drop first so a changed parameter signature applies cleanly - CREATE OR REPLACE alone cannot change
            // parameter types/names and would instead create a second overload. DROP ... IF EXISTS is a no-op on first create.
            builder.AppendLine(CrashReportInsertFunction.DropSql).EndCommand();
            builder.AppendLine(newSql).EndCommand();
        }
        else if (newSql is null && oldSql is not null)
            builder.AppendLine(CrashReportInsertFunction.DropSql).EndCommand();
    }
}