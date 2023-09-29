using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BUTR.CrashReportServer.Migrations
{
    /// <inheritdoc />
    public partial class Json : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "id_entity",
                columns: table => new
                {
                    file_id = table.Column<string>(type: "TEXT", nullable: false),
                    crash_report_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_id_entity", x => x.file_id);
                });

            migrationBuilder.CreateTable(
                name: "json_entity",
                columns: table => new
                {
                    file_id = table.Column<string>(type: "TEXT", nullable: false),
                    data_compressed = table.Column<byte[]>(type: "BLOB", nullable: false)
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

            migrationBuilder.Sql("""
                                 UPDATE file_entity
                                 SET name = REPLACE(name, '.html', '');
                                 """);

            migrationBuilder.Sql("""
                                 INSERT INTO id_entity(file_id, created, crash_report_id)
                                 SELECT name, created, '00000000-0000-0000-0000-000000000000'
                                 FROM file_entity;
                                 """);

            migrationBuilder.CreateIndex(
                name: "IX_id_entity_crash_report_id",
                table: "id_entity",
                column: "crash_report_id");

            migrationBuilder.AddForeignKey(
                name: "FK_file_entity_id_entity_name",
                table: "file_entity",
                column: "name",
                principalTable: "id_entity",
                principalColumn: "file_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropColumn(
                name: "created",
                table: "file_entity");

            migrationBuilder.DropColumn(
                name: "modified",
                table: "file_entity");

            migrationBuilder.DropColumn(
                name: "size_original",
                table: "file_entity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_file_entity_id_entity_name",
                table: "file_entity");

            migrationBuilder.DropTable(
                name: "json_entity");

            migrationBuilder.DropTable(
                name: "id_entity");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created",
                table: "file_entity",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "modified",
                table: "file_entity",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<long>(
                name: "size_original",
                table: "file_entity",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
