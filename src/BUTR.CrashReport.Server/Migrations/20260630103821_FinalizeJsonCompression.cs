using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BUTR.CrashReport.Server.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeJsonCompression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Safety guard: refuse to drop json_entity.data while any row is still un-backfilled.
            // For such rows `data` is the ONLY copy of the JSON, so dropping it would lose data. Abort
            // the whole migration (this runs in the migration's transaction) instead of letting EF's
            // SET NOT NULL coerce the gap into empty byte arrays. Run the Phase 2 backfill to completion
            // (data_compressed IS NULL must be 0) before deploying this migration.
            migrationBuilder.Sql(@"
DO $$
DECLARE
    leftover bigint;
BEGIN
    SELECT count(*) INTO leftover FROM json_entity WHERE data_compressed IS NULL;
    IF leftover > 0 THEN
        RAISE EXCEPTION 'FinalizeJsonCompression aborted: % json_entity row(s) still have data_compressed IS NULL. Run the compression backfill (Phase 2) to completion before dropping json_entity.data.', leftover;
    END IF;
END $$;");

            migrationBuilder.DropColumn(
                name: "data",
                table: "json_entity");

            migrationBuilder.AlterColumn<byte[]>(
                name: "data_compressed",
                table: "json_entity",
                type: "bytea",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "data_compressed",
                table: "json_entity",
                type: "bytea",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "bytea");

            migrationBuilder.AddColumn<string>(
                name: "data",
                table: "json_entity",
                type: "jsonb",
                nullable: true);
        }
    }
}
