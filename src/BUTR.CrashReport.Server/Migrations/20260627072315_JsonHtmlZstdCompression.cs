using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

using System;

#nullable disable

namespace BUTR.CrashReport.Server.Migrations
{
    /// <inheritdoc />
    public partial class JsonHtmlZstdCompression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("BUTR:CrashReportInsertFunction", "CREATE OR REPLACE FUNCTION insert_crash_report(\r\n    p_crash_report_id uuid,\r\n    p_tenant smallint,\r\n    p_version smallint,\r\n    p_created timestamp with time zone,\r\n    p_delete_token_hash bytea,\r\n    p_file_ids text[],\r\n    p_html_compressed bytea,\r\n    p_html_dict_id smallint,\r\n    p_json_compressed bytea,\r\n    p_json_dict_id smallint\r\n) RETURNS TABLE(o_file_id text, o_tenant smallint, o_created boolean)\r\nLANGUAGE plpgsql AS $func$\r\nDECLARE\r\n    v_candidate text;\r\n    v_constraint text;\r\nBEGIN\r\n    -- Idempotency: the same crash report was already stored - return its canonical file id, no new token.\r\n    SELECT r.file_id, r.tenant, false\r\n      INTO o_file_id, o_tenant, o_created\r\n      FROM report_entity r\r\n     WHERE r.crash_report_id = p_crash_report_id;\r\n    IF FOUND THEN RETURN NEXT; RETURN; END IF;\r\n\r\n    -- Try candidate ids in order until one is free within the tenant.\r\n    FOREACH v_candidate IN ARRAY p_file_ids LOOP\r\n        -- Skip ids already used as a legacy alias (static table, no runtime races) so a file id maps to one report.\r\n        CONTINUE WHEN EXISTS (SELECT 1 FROM id_alias_entity a WHERE a.file_id = v_candidate AND a.tenant = p_tenant);\r\n        BEGIN\r\n            INSERT INTO report_entity (crash_report_id, tenant, version, created, file_id, delete_token_hash)\r\n            VALUES (p_crash_report_id, p_tenant, p_version, p_created, v_candidate, p_delete_token_hash);\r\n\r\n            IF p_html_compressed IS NOT NULL THEN\r\n                INSERT INTO html_entity (crash_report_id, data_compressed, dict_id)\r\n                VALUES (p_crash_report_id, p_html_compressed, p_html_dict_id);\r\n            END IF;\r\n            IF p_json_compressed IS NOT NULL THEN\r\n                -- Payloads arrive already zstd-compressed as bytea; p_*_dict_id is NULL when no dictionary was used.\r\n                INSERT INTO json_entity (crash_report_id, data_compressed, dict_id)\r\n                VALUES (p_crash_report_id, p_json_compressed, p_json_dict_id);\r\n            END IF;\r\n\r\n            o_file_id := v_candidate; o_tenant := p_tenant; o_created := true;\r\n            RETURN NEXT; RETURN;\r\n        EXCEPTION WHEN unique_violation THEN\r\n            GET STACKED DIAGNOSTICS v_constraint = CONSTRAINT_NAME;\r\n            IF v_constraint = 'report_entity_pkey' THEN\r\n                -- A concurrent upload of the same crash report won the race; return its id idempotently.\r\n                SELECT r.file_id, r.tenant, false\r\n                  INTO o_file_id, o_tenant, o_created\r\n                  FROM report_entity r\r\n                 WHERE r.crash_report_id = p_crash_report_id;\r\n                RETURN NEXT; RETURN;\r\n            ELSIF v_constraint <> 'report_entity_file_id_tenant_idx' THEN\r\n                RAISE; -- not a file_id clash within the tenant; propagate\r\n            END IF;\r\n            -- file_id taken within the tenant: try the next candidate\r\n        END;\r\n    END LOOP;\r\n\r\n    RAISE EXCEPTION 'no free file_id for tenant % in % candidates', p_tenant, coalesce(array_length(p_file_ids, 1), 0)\r\n        USING ERRCODE = 'unique_violation';\r\nEND;\r\n$func$;");

            migrationBuilder.AlterColumn<string>(
                name: "data",
                table: "json_entity",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb");

            migrationBuilder.AddColumn<byte[]>(
                name: "data_compressed",
                table: "json_entity",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "dict_id",
                table: "json_entity",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "dict_id",
                table: "html_entity",
                type: "smallint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "compression_dictionary",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant = table.Column<byte>(type: "smallint", nullable: false),
                    kind = table.Column<byte>(type: "smallint", nullable: false),
                    version = table.Column<byte>(type: "smallint", nullable: false),
                    bytes = table.Column<byte[]>(type: "bytea", nullable: false),
                    created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("compression_dictionary_pkey", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_json_entity_dict_id",
                table: "json_entity",
                column: "dict_id");

            migrationBuilder.CreateIndex(
                name: "IX_html_entity_dict_id",
                table: "html_entity",
                column: "dict_id");

            migrationBuilder.CreateIndex(
                name: "compression_dictionary_active_idx",
                table: "compression_dictionary",
                columns: new[] { "tenant", "kind", "version" },
                unique: true,
                filter: "is_active");

            migrationBuilder.AddForeignKey(
                name: "html_entity_compression_dictionary_fkey",
                table: "html_entity",
                column: "dict_id",
                principalTable: "compression_dictionary",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "json_entity_compression_dictionary_fkey",
                table: "json_entity",
                column: "dict_id",
                principalTable: "compression_dictionary",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // These columns hold already-compressed (zstd) bytes, so tell TOAST not to spend cycles trying to pglz
            // them again: STORAGE EXTERNAL keeps out-of-line storage but disables compression. Applies to values
            // written/rewritten after this point (the backfill rewrites the existing rows).
            migrationBuilder.Sql("ALTER TABLE json_entity ALTER COLUMN data_compressed SET STORAGE EXTERNAL;");
            migrationBuilder.Sql("ALTER TABLE html_entity ALTER COLUMN data_compressed SET STORAGE EXTERNAL;");
            migrationBuilder.Sql("ALTER TABLE compression_dictionary ALTER COLUMN bytes SET STORAGE EXTERNAL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore html_entity.data_compressed to the default bytea storage (it predates this migration as gzip).
            migrationBuilder.Sql("ALTER TABLE html_entity ALTER COLUMN data_compressed SET STORAGE EXTENDED;");

            migrationBuilder.DropForeignKey(
                name: "html_entity_compression_dictionary_fkey",
                table: "html_entity");

            migrationBuilder.DropForeignKey(
                name: "json_entity_compression_dictionary_fkey",
                table: "json_entity");

            migrationBuilder.DropTable(
                name: "compression_dictionary");

            migrationBuilder.DropIndex(
                name: "IX_json_entity_dict_id",
                table: "json_entity");

            migrationBuilder.DropIndex(
                name: "IX_html_entity_dict_id",
                table: "html_entity");

            migrationBuilder.DropColumn(
                name: "data_compressed",
                table: "json_entity");

            migrationBuilder.DropColumn(
                name: "dict_id",
                table: "json_entity");

            migrationBuilder.DropColumn(
                name: "dict_id",
                table: "html_entity");

            // Restore the previous (Canonical) function body on rollback. Without this the differ would only DROP the
            // function (the pre-schema snapshot model emits no function annotation, since CanBuild now requires the new
            // columns), leaving the rolled-back image with no insert_crash_report. Recreating it keeps Down correct.
            migrationBuilder.AlterDatabase()
                .Annotation("BUTR:CrashReportInsertFunction", "CREATE OR REPLACE FUNCTION insert_crash_report(\n    p_crash_report_id uuid,\n    p_tenant smallint,\n    p_version smallint,\n    p_created timestamp with time zone,\n    p_delete_token_hash bytea,\n    p_file_ids text[],\n    p_html_compressed bytea,\n    p_json text\n) RETURNS TABLE(o_file_id text, o_tenant smallint, o_created boolean)\nLANGUAGE plpgsql AS $func$\nDECLARE\n    v_candidate text;\n    v_constraint text;\nBEGIN\n    -- Idempotency: the same crash report was already stored - return its canonical file id, no new token.\n    SELECT r.file_id, r.tenant, false\n      INTO o_file_id, o_tenant, o_created\n      FROM report_entity r\n     WHERE r.crash_report_id = p_crash_report_id;\n    IF FOUND THEN RETURN NEXT; RETURN; END IF;\n\n    -- Try candidate ids in order until one is free within the tenant.\n    FOREACH v_candidate IN ARRAY p_file_ids LOOP\n        -- Skip ids already used as a legacy alias (static table, no runtime races) so a file id maps to one report.\n        CONTINUE WHEN EXISTS (SELECT 1 FROM id_alias_entity a WHERE a.file_id = v_candidate AND a.tenant = p_tenant);\n        BEGIN\n            INSERT INTO report_entity (crash_report_id, tenant, version, created, file_id, delete_token_hash)\n            VALUES (p_crash_report_id, p_tenant, p_version, p_created, v_candidate, p_delete_token_hash);\n\n            IF p_html_compressed IS NOT NULL THEN\n                INSERT INTO html_entity (crash_report_id, data_compressed)\n                VALUES (p_crash_report_id, p_html_compressed);\n            END IF;\n            IF p_json IS NOT NULL THEN\n                -- p_json arrives as text (what EF sends for a CLR string); cast to the column's type.\n                INSERT INTO json_entity (crash_report_id, data)\n                VALUES (p_crash_report_id, p_json::jsonb);\n            END IF;\n\n            o_file_id := v_candidate; o_tenant := p_tenant; o_created := true;\n            RETURN NEXT; RETURN;\n        EXCEPTION WHEN unique_violation THEN\n            GET STACKED DIAGNOSTICS v_constraint = CONSTRAINT_NAME;\n            IF v_constraint = 'report_entity_pkey' THEN\n                -- A concurrent upload of the same crash report won the race; return its id idempotently.\n                SELECT r.file_id, r.tenant, false\n                  INTO o_file_id, o_tenant, o_created\n                  FROM report_entity r\n                 WHERE r.crash_report_id = p_crash_report_id;\n                RETURN NEXT; RETURN;\n            ELSIF v_constraint <> 'report_entity_file_id_tenant_idx' THEN\n                RAISE; -- not a file_id clash within the tenant; propagate\n            END IF;\n            -- file_id taken within the tenant: try the next candidate\n        END;\n    END LOOP;\n\n    RAISE EXCEPTION 'no free file_id for tenant % in % candidates', p_tenant, coalesce(array_length(p_file_ids, 1), 0)\n        USING ERRCODE = 'unique_violation';\nEND;\n$func$;")
                .OldAnnotation("BUTR:CrashReportInsertFunction", "CREATE OR REPLACE FUNCTION insert_crash_report(\r\n    p_crash_report_id uuid,\r\n    p_tenant smallint,\r\n    p_version smallint,\r\n    p_created timestamp with time zone,\r\n    p_delete_token_hash bytea,\r\n    p_file_ids text[],\r\n    p_html_compressed bytea,\r\n    p_html_dict_id smallint,\r\n    p_json_compressed bytea,\r\n    p_json_dict_id smallint\r\n) RETURNS TABLE(o_file_id text, o_tenant smallint, o_created boolean)\r\nLANGUAGE plpgsql AS $func$\r\nDECLARE\r\n    v_candidate text;\r\n    v_constraint text;\r\nBEGIN\r\n    -- Idempotency: the same crash report was already stored - return its canonical file id, no new token.\r\n    SELECT r.file_id, r.tenant, false\r\n      INTO o_file_id, o_tenant, o_created\r\n      FROM report_entity r\r\n     WHERE r.crash_report_id = p_crash_report_id;\r\n    IF FOUND THEN RETURN NEXT; RETURN; END IF;\r\n\r\n    -- Try candidate ids in order until one is free within the tenant.\r\n    FOREACH v_candidate IN ARRAY p_file_ids LOOP\r\n        -- Skip ids already used as a legacy alias (static table, no runtime races) so a file id maps to one report.\r\n        CONTINUE WHEN EXISTS (SELECT 1 FROM id_alias_entity a WHERE a.file_id = v_candidate AND a.tenant = p_tenant);\r\n        BEGIN\r\n            INSERT INTO report_entity (crash_report_id, tenant, version, created, file_id, delete_token_hash)\r\n            VALUES (p_crash_report_id, p_tenant, p_version, p_created, v_candidate, p_delete_token_hash);\r\n\r\n            IF p_html_compressed IS NOT NULL THEN\r\n                INSERT INTO html_entity (crash_report_id, data_compressed, dict_id)\r\n                VALUES (p_crash_report_id, p_html_compressed, p_html_dict_id);\r\n            END IF;\r\n            IF p_json_compressed IS NOT NULL THEN\r\n                -- Payloads arrive already zstd-compressed as bytea; p_*_dict_id is NULL when no dictionary was used.\r\n                INSERT INTO json_entity (crash_report_id, data_compressed, dict_id)\r\n                VALUES (p_crash_report_id, p_json_compressed, p_json_dict_id);\r\n            END IF;\r\n\r\n            o_file_id := v_candidate; o_tenant := p_tenant; o_created := true;\r\n            RETURN NEXT; RETURN;\r\n        EXCEPTION WHEN unique_violation THEN\r\n            GET STACKED DIAGNOSTICS v_constraint = CONSTRAINT_NAME;\r\n            IF v_constraint = 'report_entity_pkey' THEN\r\n                -- A concurrent upload of the same crash report won the race; return its id idempotently.\r\n                SELECT r.file_id, r.tenant, false\r\n                  INTO o_file_id, o_tenant, o_created\r\n                  FROM report_entity r\r\n                 WHERE r.crash_report_id = p_crash_report_id;\r\n                RETURN NEXT; RETURN;\r\n            ELSIF v_constraint <> 'report_entity_file_id_tenant_idx' THEN\r\n                RAISE; -- not a file_id clash within the tenant; propagate\r\n            END IF;\r\n            -- file_id taken within the tenant: try the next candidate\r\n        END;\r\n    END LOOP;\r\n\r\n    RAISE EXCEPTION 'no free file_id for tenant % in % candidates', p_tenant, coalesce(array_length(p_file_ids, 1), 0)\r\n        USING ERRCODE = 'unique_violation';\r\nEND;\r\n$func$;");

            // Restore data to NOT NULL with NO default (its pre-Up state). EF's scaffolder defaults this to
            // defaultValue: "", but "" is invalid jsonb and makes the whole Down fail - so it is removed here.
            migrationBuilder.AlterColumn<string>(
                name: "data",
                table: "json_entity",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);
        }
    }
}