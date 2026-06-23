using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BUTR.CrashReport.Server.Migrations
{
    /// <inheritdoc />
    public partial class DeleteToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "delete_token_hash",
                table: "report_entity",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "delete_token_hash",
                table: "report_entity");
        }
    }
}
