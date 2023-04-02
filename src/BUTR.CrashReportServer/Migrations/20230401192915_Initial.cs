using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BUTR.CrashReportServer.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "file_entity",
                columns: table => new
                {
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    modified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    size_original = table.Column<long>(type: "INTEGER", nullable: false),
                    data_compressed = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("file_entity_pkey", x => x.name);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "file_entity");
        }
    }
}
