using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BUTR.CrashReport.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddBackfillPartialIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_json_backfill",
                table: "json_entity",
                column: "crash_report_id",
                filter: "dict_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_html_backfill",
                table: "html_entity",
                column: "crash_report_id",
                filter: "dict_id IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_json_backfill",
                table: "json_entity");

            migrationBuilder.DropIndex(
                name: "ix_html_backfill",
                table: "html_entity");
        }
    }
}
