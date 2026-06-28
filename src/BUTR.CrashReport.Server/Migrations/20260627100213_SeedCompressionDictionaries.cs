using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BUTR.CrashReport.Server.Migrations
{
    /// <summary>
    /// Intentionally a no-op. Seeding the embedded compression dictionaries used to happen here as inline
    /// <c>INSERT ... decode('&lt;hex&gt;','hex')</c> statements, but the dictionaries are multi-megabyte blobs and EF Core
    /// logs the full command text - that flooded the deploy log (tens of MB of hex per dictionary on a single line) and
    /// stalled the streamed deploy. Seeding now runs as a parameterized, idempotent post-migrate step
    /// (<see cref="BUTR.CrashReport.Server.Extensions.DictionarySeeder"/>, invoked after migrations in the 'migrate'
    /// command) where the byte[] travels as a redacted parameter (<c>@p='?'</c>) instead of an inline literal.
    /// This migration is kept so its id remains in <c>__EFMigrationsHistory</c> for environments that already applied it.
    /// </summary>
    public partial class SeedCompressionDictionaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) { }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
