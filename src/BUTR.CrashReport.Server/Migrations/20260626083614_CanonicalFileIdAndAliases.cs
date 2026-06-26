using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BUTR.CrashReport.Server.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalFileIdAndAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("BUTR:CrashReportInsertFunction", "CREATE OR REPLACE FUNCTION insert_crash_report(\n    p_crash_report_id uuid,\n    p_tenant smallint,\n    p_version smallint,\n    p_created timestamp with time zone,\n    p_delete_token_hash bytea,\n    p_file_ids text[],\n    p_html_compressed bytea,\n    p_json text\n) RETURNS TABLE(o_file_id text, o_tenant smallint, o_created boolean)\nLANGUAGE plpgsql AS $func$\nDECLARE\n    v_candidate text;\n    v_constraint text;\nBEGIN\n    -- Idempotency: the same crash report was already stored - return its canonical file id, no new token.\n    SELECT r.file_id, r.tenant, false\n      INTO o_file_id, o_tenant, o_created\n      FROM report_entity r\n     WHERE r.crash_report_id = p_crash_report_id;\n    IF FOUND THEN RETURN NEXT; RETURN; END IF;\n\n    -- Try candidate ids in order until one is free within the tenant.\n    FOREACH v_candidate IN ARRAY p_file_ids LOOP\n        -- Skip ids already used as a legacy alias (static table, no runtime races) so a file id maps to one report.\n        CONTINUE WHEN EXISTS (SELECT 1 FROM id_alias_entity a WHERE a.file_id = v_candidate AND a.tenant = p_tenant);\n        BEGIN\n            INSERT INTO report_entity (crash_report_id, tenant, version, created, file_id, delete_token_hash)\n            VALUES (p_crash_report_id, p_tenant, p_version, p_created, v_candidate, p_delete_token_hash);\n\n            IF p_html_compressed IS NOT NULL THEN\n                INSERT INTO html_entity (crash_report_id, data_compressed)\n                VALUES (p_crash_report_id, p_html_compressed);\n            END IF;\n            IF p_json IS NOT NULL THEN\n                -- p_json arrives as text (what EF sends for a CLR string); cast to the column's type.\n                INSERT INTO json_entity (crash_report_id, data)\n                VALUES (p_crash_report_id, p_json::jsonb);\n            END IF;\n\n            o_file_id := v_candidate; o_tenant := p_tenant; o_created := true;\n            RETURN NEXT; RETURN;\n        EXCEPTION WHEN unique_violation THEN\n            GET STACKED DIAGNOSTICS v_constraint = CONSTRAINT_NAME;\n            IF v_constraint = 'report_entity_pkey' THEN\n                -- A concurrent upload of the same crash report won the race; return its id idempotently.\n                SELECT r.file_id, r.tenant, false\n                  INTO o_file_id, o_tenant, o_created\n                  FROM report_entity r\n                 WHERE r.crash_report_id = p_crash_report_id;\n                RETURN NEXT; RETURN;\n            ELSIF v_constraint <> 'report_entity_file_id_tenant_idx' THEN\n                RAISE; -- not a file_id clash within the tenant; propagate\n            END IF;\n            -- file_id taken within the tenant: try the next candidate\n        END;\n    END LOOP;\n\n    RAISE EXCEPTION 'no free file_id for tenant % in % candidates', p_tenant, coalesce(array_length(p_file_ids, 1), 0)\n        USING ERRCODE = 'unique_violation';\nEND;\n$func$;");

            migrationBuilder.AddColumn<string>(
                name: "file_id",
                table: "report_entity",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "id_alias_entity",
                columns: table => new
                {
                    file_id = table.Column<string>(type: "text", nullable: false),
                    crash_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("id_alias_entity_pkey", x => x.file_id);
                    table.ForeignKey(
                        name: "report_entity_id_alias_entity_fkey",
                        column: x => x.crash_report_id,
                        principalTable: "report_entity",
                        principalColumn: "crash_report_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_id_alias_entity_crash_report_id",
                table: "id_alias_entity",
                column: "crash_report_id");

            migrationBuilder.CreateIndex(
                name: "id_alias_entity_file_id_tenant_idx",
                table: "id_alias_entity",
                columns: new[] { "file_id", "tenant" },
                unique: true);

            // Migrate existing data from id_entity (1:many file_id -> report) into the new shape:
            //  - the canonical (smallest) file_id per report becomes report_entity.file_id
            //  - the remaining file_ids become legacy aliases in id_alias_entity (tenant from report_entity)
            // Precondition: every report has at least one id_entity row, else its file_id stays '' and the unique
            // (file_id, tenant) index below fails. Verify on prod:
            //   SELECT count(*) FROM report_entity r WHERE NOT EXISTS (SELECT 1 FROM id_entity i WHERE i.crash_report_id = r.crash_report_id);
            migrationBuilder.Sql(
                """
                UPDATE report_entity r
                SET file_id = sub.min_file_id
                FROM (SELECT crash_report_id, MIN(file_id) AS min_file_id FROM id_entity GROUP BY crash_report_id) sub
                WHERE sub.crash_report_id = r.crash_report_id;

                INSERT INTO id_alias_entity (file_id, crash_report_id, tenant)
                SELECT i.file_id, i.crash_report_id, r.tenant
                FROM id_entity i
                JOIN report_entity r ON r.crash_report_id = i.crash_report_id
                WHERE i.file_id <> (SELECT MIN(i2.file_id) FROM id_entity i2 WHERE i2.crash_report_id = i.crash_report_id);
                """);

            // After backfill so the canonical file_ids are populated (otherwise all-'' rows would collide).
            migrationBuilder.CreateIndex(
                name: "report_entity_file_id_tenant_idx",
                table: "report_entity",
                columns: new[] { "file_id", "tenant" },
                unique: true);

            // id_entity's data has been moved out; drop it last.
            migrationBuilder.DropTable(
                name: "id_entity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .OldAnnotation("BUTR:CrashReportInsertFunction", "CREATE OR REPLACE FUNCTION insert_crash_report(\n    p_crash_report_id uuid,\n    p_tenant smallint,\n    p_version smallint,\n    p_created timestamp with time zone,\n    p_delete_token_hash bytea,\n    p_file_ids text[],\n    p_html_compressed bytea,\n    p_json text\n) RETURNS TABLE(o_file_id text, o_tenant smallint, o_created boolean)\nLANGUAGE plpgsql AS $func$\nDECLARE\n    v_candidate text;\n    v_constraint text;\nBEGIN\n    -- Idempotency: the same crash report was already stored - return its canonical file id, no new token.\n    SELECT r.file_id, r.tenant, false\n      INTO o_file_id, o_tenant, o_created\n      FROM report_entity r\n     WHERE r.crash_report_id = p_crash_report_id;\n    IF FOUND THEN RETURN NEXT; RETURN; END IF;\n\n    -- Try candidate ids in order until one is free within the tenant.\n    FOREACH v_candidate IN ARRAY p_file_ids LOOP\n        -- Skip ids already used as a legacy alias (static table, no runtime races) so a file id maps to one report.\n        CONTINUE WHEN EXISTS (SELECT 1 FROM id_alias_entity a WHERE a.file_id = v_candidate AND a.tenant = p_tenant);\n        BEGIN\n            INSERT INTO report_entity (crash_report_id, tenant, version, created, file_id, delete_token_hash)\n            VALUES (p_crash_report_id, p_tenant, p_version, p_created, v_candidate, p_delete_token_hash);\n\n            IF p_html_compressed IS NOT NULL THEN\n                INSERT INTO html_entity (crash_report_id, data_compressed)\n                VALUES (p_crash_report_id, p_html_compressed);\n            END IF;\n            IF p_json IS NOT NULL THEN\n                -- p_json arrives as text (what EF sends for a CLR string); cast to the column's type.\n                INSERT INTO json_entity (crash_report_id, data)\n                VALUES (p_crash_report_id, p_json::jsonb);\n            END IF;\n\n            o_file_id := v_candidate; o_tenant := p_tenant; o_created := true;\n            RETURN NEXT; RETURN;\n        EXCEPTION WHEN unique_violation THEN\n            GET STACKED DIAGNOSTICS v_constraint = CONSTRAINT_NAME;\n            IF v_constraint = 'report_entity_pkey' THEN\n                -- A concurrent upload of the same crash report won the race; return its id idempotently.\n                SELECT r.file_id, r.tenant, false\n                  INTO o_file_id, o_tenant, o_created\n                  FROM report_entity r\n                 WHERE r.crash_report_id = p_crash_report_id;\n                RETURN NEXT; RETURN;\n            ELSIF v_constraint <> 'report_entity_file_id_tenant_idx' THEN\n                RAISE; -- not a file_id clash within the tenant; propagate\n            END IF;\n            -- file_id taken within the tenant: try the next candidate\n        END;\n    END LOOP;\n\n    RAISE EXCEPTION 'no free file_id for tenant % in % candidates', p_tenant, coalesce(array_length(p_file_ids, 1), 0)\n        USING ERRCODE = 'unique_violation';\nEND;\n$func$;");

            migrationBuilder.CreateTable(
                name: "id_entity",
                columns: table => new
                {
                    crash_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    // file_id is the key here (1:many: a report could have several), matching the pre-migration shape.
                    table.PrimaryKey("id_entity_pkey", x => x.file_id);
                    table.ForeignKey(
                        name: "report_entity_id_entity_fkey",
                        column: x => x.crash_report_id,
                        principalTable: "report_entity",
                        principalColumn: "crash_report_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "id_entity_file_id_idx",
                table: "id_entity",
                column: "file_id");

            // Restore id_entity from the canonical report_entity.file_id plus the aliases, then drop the new shape.
            migrationBuilder.Sql(
                """
                INSERT INTO id_entity (crash_report_id, file_id)
                SELECT crash_report_id, file_id FROM report_entity;
                INSERT INTO id_entity (crash_report_id, file_id)
                SELECT crash_report_id, file_id FROM id_alias_entity;
                """);

            migrationBuilder.DropTable(
                name: "id_alias_entity");

            migrationBuilder.DropIndex(
                name: "report_entity_file_id_tenant_idx",
                table: "report_entity");

            migrationBuilder.DropColumn(
                name: "file_id",
                table: "report_entity");
        }
    }
}
