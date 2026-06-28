using Microsoft.EntityFrameworkCore.Migrations;

using System;
using System.Reflection;
using System.Text.RegularExpressions;

#nullable disable

namespace BUTR.CrashReport.Server.Migrations
{
    /// <summary>
    /// Seeds the compression dictionaries that are baked into the migrations assembly as embedded <c>.bin</c> resources
    /// (see <c>Migrations/Dictionaries/README.md</c>) as the <b>active</b> dictionaries, so new uploads compress with a
    /// dictionary from the first request. A <b>no-op when no dictionary files are embedded</b> - the schema migration can
    /// ship before the dictionaries are trained; add the files and redeploy (or create dictionaries via the web service).
    /// </summary>
    public partial class SeedCompressionDictionaries : Migration
    {
        // Embedded resource name -> ...Migrations.Dictionaries.dict.t{tenant}.{json|html}.v{version}.bin
        private static readonly Regex DictName = new(@"\.Migrations\.Dictionaries\.dict\.t(\d+)\.(json|html)\.v(\d+)\.bin$", RegexOptions.Compiled);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var assembly = typeof(SeedCompressionDictionaries).Assembly;
            foreach (var resource in assembly.GetManifestResourceNames())
            {
                var match = DictName.Match(resource);
                if (!match.Success) continue;

                var tenant = short.Parse(match.Groups[1].Value);
                var kind = match.Groups[2].Value == "html" ? 1 : 0;
                var version = short.Parse(match.Groups[3].Value);

                using var stream = assembly.GetManifestResourceStream(resource)!;
                using var ms = new System.IO.MemoryStream();
                stream.CopyTo(ms);
                var hex = Convert.ToHexString(ms.ToArray());

                // Demote any existing active dict for this key (the partial unique index allows only one), then insert
                // the baked one active. now() is fine - dictionaries are append-only and identified by their surrogate id.
                migrationBuilder.Sql($"UPDATE compression_dictionary SET is_active = false WHERE tenant = {tenant} AND kind = {kind} AND version = {version} AND is_active;");
                migrationBuilder.Sql($"INSERT INTO compression_dictionary (tenant, kind, version, bytes, created, is_active) VALUES ({tenant}, {kind}, {version}, decode('{hex}', 'hex'), now(), true);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var assembly = typeof(SeedCompressionDictionaries).Assembly;
            foreach (var resource in assembly.GetManifestResourceNames())
            {
                var match = DictName.Match(resource);
                if (!match.Success) continue;

                var tenant = short.Parse(match.Groups[1].Value);
                var kind = match.Groups[2].Value == "html" ? 1 : 0;
                var version = short.Parse(match.Groups[3].Value);

                // Only remove a seeded dict that nothing references yet - if rows already use it, it is in-use data and
                // must be kept (so rollback never breaks on the FK).
                migrationBuilder.Sql($"""
                    DELETE FROM compression_dictionary cd
                    WHERE cd.tenant = {tenant} AND cd.kind = {kind} AND cd.version = {version} AND cd.is_active
                      AND NOT EXISTS (SELECT 1 FROM json_entity j WHERE j.dict_id = cd.id)
                      AND NOT EXISTS (SELECT 1 FROM html_entity h WHERE h.dict_id = cd.id);
                    """);
            }
        }
    }
}