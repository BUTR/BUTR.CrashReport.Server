using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BUTR.CrashReportServer.Migrations
{
    /// <inheritdoc />
    public partial class FileEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_file_entity_id_entity_name",
                table: "file_entity");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "file_entity",
                newName: "file_id");

            migrationBuilder.AddForeignKey(
                name: "FK_file_entity_id_entity_file_id",
                table: "file_entity",
                column: "file_id",
                principalTable: "id_entity",
                principalColumn: "file_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_file_entity_id_entity_file_id",
                table: "file_entity");

            migrationBuilder.RenameColumn(
                name: "file_id",
                table: "file_entity",
                newName: "name");

            migrationBuilder.AddForeignKey(
                name: "FK_file_entity_id_entity_name",
                table: "file_entity",
                column: "name",
                principalTable: "id_entity",
                principalColumn: "file_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
