using Microsoft.EntityFrameworkCore.Migrations;

using System;

#nullable disable

namespace BUTR.CrashReport.Server.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "id_entity",
                columns: table => new
                {
                    file_id = table.Column<string>(type: "text", nullable: false),
                    crash_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<byte>(type: "smallint", nullable: false),
                    created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_id_entity", x => x.file_id);
                });

            migrationBuilder.CreateTable(
                name: "file_entity",
                columns: table => new
                {
                    file_id = table.Column<string>(type: "text", nullable: false),
                    data_compressed = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("file_entity_pkey", x => x.file_id);
                    table.ForeignKey(
                        name: "FK_file_entity_id_entity_file_id",
                        column: x => x.file_id,
                        principalTable: "id_entity",
                        principalColumn: "file_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "json_entity",
                columns: table => new
                {
                    file_id = table.Column<string>(type: "text", nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_json_entity", x => x.file_id);
                    table.ForeignKey(
                        name: "FK_json_entity_id_entity_file_id",
                        column: x => x.file_id,
                        principalTable: "id_entity",
                        principalColumn: "file_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_id_entity_crash_report_id",
                table: "id_entity",
                column: "crash_report_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "file_entity");

            migrationBuilder.DropTable(
                name: "json_entity");

            migrationBuilder.DropTable(
                name: "id_entity");
        }
    }
}