using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BUTR.CrashReport.Server.Migrations
{
    /// <inheritdoc />
    public partial class OldHtmlZstdCompression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "dict_id",
                table: "old_html_entity",
                type: "smallint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_old_html_entity_dict_id",
                table: "old_html_entity",
                column: "dict_id");

            migrationBuilder.AddForeignKey(
                name: "old_html_entity_compression_dictionary_fkey",
                table: "old_html_entity",
                column: "dict_id",
                principalTable: "compression_dictionary",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Archived originals are rewritten to already-compressed zstd bytes, so tell TOAST not to pglz them again.
            // Applies to values written after this point (the backfill rewrites the existing gzip rows).
            migrationBuilder.Sql("ALTER TABLE old_html_entity ALTER COLUMN data_compressed SET STORAGE EXTERNAL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the default bytea storage (the column predates this migration as gzip with EXTENDED storage).
            migrationBuilder.Sql("ALTER TABLE old_html_entity ALTER COLUMN data_compressed SET STORAGE EXTENDED;");

            migrationBuilder.DropForeignKey(
                name: "old_html_entity_compression_dictionary_fkey",
                table: "old_html_entity");

            migrationBuilder.DropIndex(
                name: "IX_old_html_entity_dict_id",
                table: "old_html_entity");

            migrationBuilder.DropColumn(
                name: "dict_id",
                table: "old_html_entity");
        }
    }
}