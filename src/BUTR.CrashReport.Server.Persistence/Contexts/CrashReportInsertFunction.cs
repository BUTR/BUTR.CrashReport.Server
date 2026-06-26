using BUTR.CrashReport.Server.Models.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using System;
using System.Collections.Generic;
using System.Linq;

namespace BUTR.CrashReport.Server.Contexts;

/// <summary>
/// Builds the <c>insert_crash_report</c> PL/pgSQL function entirely from the EF relational model - table names,
/// column names, store types, and constraint names all come from the configured mappings, so the function tracks
/// them with no duplicated literals. A small marker annotation (<see cref="MarkerAnnotationName"/>) is set on the
/// model in <c>OnModelCreating</c>; the relational annotation provider turns it into the generated SQL under
/// <see cref="AnnotationName"/>, and the migrations differ recreates the function whenever that SQL changes.
///
/// The function does generate-retry-insert for the whole report in a single round trip: it tries each candidate
/// file id until one is free within the tenant (free across both the canonical report_entity.file_id and the legacy
/// id_alias_entity), inserts the report row (with its canonical file id) plus optional html/json, and returns the
/// assigned id. Re-uploads of the same crash report are idempotent (return the existing canonical id).
/// </summary>
public static class CrashReportInsertFunction
{
    public const string AnnotationName = "BUTR:CrashReportInsertFunction";
    public const string MarkerAnnotationName = AnnotationName + ":Enabled";
    public const string FunctionName = "insert_crash_report";

    // Exactly one function of this name exists, so it can be dropped by name without the argument list.
    public static string DropSql => $"DROP FUNCTION IF EXISTS {FunctionName};";

    /// <summary>
    /// Whether the model has the schema the function needs. Old migration snapshots may carry the marker annotation
    /// while predating this schema (e.g. no IdAliasEntity); the annotation provider runs against those models too, so
    /// it must skip them rather than fail.
    /// </summary>
    public static bool CanBuild(IRelationalModel relationalModel)
    {
        var model = relationalModel.Model;
        return model.FindEntityType(typeof(ReportEntity))?.FindProperty(nameof(ReportEntity.FileId)) is not null
            && model.FindEntityType(typeof(IdAliasEntity)) is not null
            && model.FindEntityType(typeof(HtmlEntity)) is not null
            && model.FindEntityType(typeof(JsonEntity)) is not null;
    }

    public static string BuildCreateSql(IRelationalModel relationalModel)
    {
        var report = Resolve<ReportEntity>(relationalModel);
        var alias = Resolve<IdAliasEntity>(relationalModel);
        var html = Resolve<HtmlEntity>(relationalModel);
        var json = Resolve<JsonEntity>(relationalModel);

        // Single source of truth for which columns each INSERT writes (order matches the VALUES lists below).
        string[] reportColumns = [nameof(ReportEntity.CrashReportId), nameof(ReportEntity.Tenant), nameof(ReportEntity.Version), nameof(ReportEntity.Created), nameof(ReportEntity.FileId), nameof(ReportEntity.DeleteTokenHash)];
        string[] htmlColumns = [nameof(HtmlEntity.CrashReportId), nameof(HtmlEntity.DataCompressed)];
        string[] jsonColumns = [nameof(JsonEntity.CrashReportId), nameof(JsonEntity.Json)];

        // Fail fast (at model build / migration scaffold) if a touched table gained a required column the function
        // would not populate - otherwise that surfaces only as a runtime insert error against Postgres.
        ValidateCoverage(report, reportColumns);
        ValidateCoverage(html, htmlColumns);
        ValidateCoverage(json, jsonColumns);

        var fileIdType = report.Type(nameof(ReportEntity.FileId));
        var reportFileIdIndex = report.UniqueIndexName(nameof(ReportEntity.FileId), nameof(ReportEntity.Tenant));

        return $$"""
            CREATE OR REPLACE FUNCTION {{FunctionName}}(
                p_crash_report_id {{report.Type(nameof(ReportEntity.CrashReportId))}},
                p_tenant {{report.Type(nameof(ReportEntity.Tenant))}},
                p_version {{report.Type(nameof(ReportEntity.Version))}},
                p_created {{report.Type(nameof(ReportEntity.Created))}},
                p_delete_token_hash {{report.Type(nameof(ReportEntity.DeleteTokenHash))}},
                p_file_ids {{fileIdType}}[],
                p_html_compressed {{html.Type(nameof(HtmlEntity.DataCompressed))}},
                p_json text
            ) RETURNS TABLE(o_file_id {{fileIdType}}, o_tenant {{report.Type(nameof(ReportEntity.Tenant))}}, o_created boolean)
            LANGUAGE plpgsql AS $func$
            DECLARE
                v_candidate {{fileIdType}};
                v_constraint text;
            BEGIN
                -- Idempotency: the same crash report was already stored - return its canonical file id, no new token.
                SELECT r.{{report.Col(nameof(ReportEntity.FileId))}}, r.{{report.Col(nameof(ReportEntity.Tenant))}}, false
                  INTO o_file_id, o_tenant, o_created
                  FROM {{report.Name}} r
                 WHERE r.{{report.Col(nameof(ReportEntity.CrashReportId))}} = p_crash_report_id;
                IF FOUND THEN RETURN NEXT; RETURN; END IF;

                -- Try candidate ids in order until one is free within the tenant.
                FOREACH v_candidate IN ARRAY p_file_ids LOOP
                    -- Skip ids already used as a legacy alias (static table, no runtime races) so a file id maps to one report.
                    CONTINUE WHEN EXISTS (SELECT 1 FROM {{alias.Name}} a WHERE a.{{alias.Col(nameof(IdAliasEntity.FileId))}} = v_candidate AND a.{{alias.Col(nameof(IdAliasEntity.Tenant))}} = p_tenant);
                    BEGIN
                        INSERT INTO {{report.Name}} ({{report.Cols(reportColumns)}})
                        VALUES (p_crash_report_id, p_tenant, p_version, p_created, v_candidate, p_delete_token_hash);

                        IF p_html_compressed IS NOT NULL THEN
                            INSERT INTO {{html.Name}} ({{html.Cols(htmlColumns)}})
                            VALUES (p_crash_report_id, p_html_compressed);
                        END IF;
                        IF p_json IS NOT NULL THEN
                            -- p_json arrives as text (what EF sends for a CLR string); cast to the column's type.
                            INSERT INTO {{json.Name}} ({{json.Cols(jsonColumns)}})
                            VALUES (p_crash_report_id, p_json::{{json.Type(nameof(JsonEntity.Json))}});
                        END IF;

                        o_file_id := v_candidate; o_tenant := p_tenant; o_created := true;
                        RETURN NEXT; RETURN;
                    EXCEPTION WHEN unique_violation THEN
                        GET STACKED DIAGNOSTICS v_constraint = CONSTRAINT_NAME;
                        IF v_constraint = '{{report.PrimaryKeyName}}' THEN
                            -- A concurrent upload of the same crash report won the race; return its id idempotently.
                            SELECT r.{{report.Col(nameof(ReportEntity.FileId))}}, r.{{report.Col(nameof(ReportEntity.Tenant))}}, false
                              INTO o_file_id, o_tenant, o_created
                              FROM {{report.Name}} r
                             WHERE r.{{report.Col(nameof(ReportEntity.CrashReportId))}} = p_crash_report_id;
                            RETURN NEXT; RETURN;
                        ELSIF v_constraint <> '{{reportFileIdIndex}}' THEN
                            RAISE; -- not a file_id clash within the tenant; propagate
                        END IF;
                        -- file_id taken within the tenant: try the next candidate
                    END;
                END LOOP;

                RAISE EXCEPTION 'no free file_id for tenant % in % candidates', p_tenant, coalesce(array_length(p_file_ids, 1), 0)
                    USING ERRCODE = 'unique_violation';
            END;
            $func$;
            """;
    }

    // Throws if the table has a required column (NOT NULL, no default, not store-generated) that the function's
    // INSERT does not populate. Turns a would-be runtime insert failure into an immediate model-build error.
    private static void ValidateCoverage(TableRef table, string[] insertedProperties)
    {
        var inserted = insertedProperties.Select(table.Col).ToHashSet();
        var missing = table.RequiredColumnNames().Where(c => !inserted.Contains(c)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException(
                $"{FunctionName} would not populate required column(s) [{string.Join(", ", missing)}] on \"{table.Name}\". " +
                "Add them to the INSERT in CrashReportInsertFunction.BuildCreateSql, or give them a database default.");
    }

    private static TableRef Resolve<T>(IRelationalModel relationalModel)
    {
        var entityType = relationalModel.Model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException($"Entity {typeof(T).Name} is not mapped.");
        var store = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table) ?? throw new InvalidOperationException($"Entity {typeof(T).Name} has no table mapping.");
        var table = relationalModel.FindTable(store.Name, store.Schema) ?? throw new InvalidOperationException($"No relational table for {typeof(T).Name}.");
        return new TableRef(entityType, store, table);
    }

    private sealed class TableRef(IReadOnlyEntityType entityType, StoreObjectIdentifier store, ITable table)
    {
        public string Name => table.Name;

        public string PrimaryKeyName =>
            entityType.FindPrimaryKey()?.GetName(store)
            ?? throw new InvalidOperationException($"{entityType.DisplayName()} has no primary key.");

        public string Col(string propertyName) =>
            entityType.FindProperty(propertyName)?.GetColumnName(store)
            ?? throw new InvalidOperationException($"No column mapped for {entityType.DisplayName()}.{propertyName}.");

        public string Cols(IEnumerable<string> propertyNames) => string.Join(", ", propertyNames.Select(Col));

        public string Type(string propertyName) =>
            table.FindColumn(Col(propertyName))?.StoreType
            ?? throw new InvalidOperationException($"No store type for {entityType.DisplayName()}.{propertyName}.");

        public string UniqueIndexName(params string[] propertyNames) =>
            entityType.GetIndexes()
                .Where(x => x.IsUnique && propertyNames.All(p => x.Properties.Any(ip => ip.Name == p)) && x.Properties.Count == propertyNames.Length)
                .Select(x => x.GetDatabaseName(store))
                .FirstOrDefault()
            ?? throw new InvalidOperationException($"No unique index on ({string.Join(", ", propertyNames)}) for {entityType.DisplayName()}.");

        // Columns the function must supply a value for: NOT NULL, no database default/computed value, and not
        // store value-generated (e.g. identity). A new column matching this is what would silently break inserts.
        public IEnumerable<string> RequiredColumnNames() => table.Columns
            .Where(c => !c.IsNullable
                        && c.DefaultValue is null
                        && c.DefaultValueSql is null
                        && c.ComputedColumnSql is null
                        && !c.PropertyMappings.All(m => m.Property.ValueGenerated is ValueGenerated.OnAdd or ValueGenerated.OnAddOrUpdate))
            .Select(c => c.Name);
    }
}
