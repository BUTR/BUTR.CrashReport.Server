using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BUTR.CrashReportServer.Migrations
{
    /// <inheritdoc />
    public partial class IdEntityGenerator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "file_id",
                table: "id_entity",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "hex(randomblob(3))",
                oldClrType: typeof(string),
                oldType: "TEXT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "file_id",
                table: "id_entity",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldDefaultValueSql: "hex(randomblob(3))");
        }
    }
}
