using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BUTR.CrashReport.Server.Migrations
{
    /// <inheritdoc />
    public partial class OptionalHtmlJsonAndArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "html_entity_id_entity_fkey",
                table: "id_entity");

            migrationBuilder.DropForeignKey(
                name: "json_entity_id_entity_fkey",
                table: "id_entity");

            migrationBuilder.CreateTable(
                name: "old_html_entity",
                columns: table => new
                {
                    crash_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_compressed = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("old_html_entity_pkey", x => x.crash_report_id);
                    table.ForeignKey(
                        name: "report_entity_old_html_entity_fkey",
                        column: x => x.crash_report_id,
                        principalTable: "report_entity",
                        principalColumn: "crash_report_id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "old_html_entity");

            migrationBuilder.AddForeignKey(
                name: "html_entity_id_entity_fkey",
                table: "id_entity",
                column: "crash_report_id",
                principalTable: "html_entity",
                principalColumn: "crash_report_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "json_entity_id_entity_fkey",
                table: "id_entity",
                column: "crash_report_id",
                principalTable: "json_entity",
                principalColumn: "crash_report_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
