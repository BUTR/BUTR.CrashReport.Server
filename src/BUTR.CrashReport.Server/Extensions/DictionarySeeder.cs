using BUTR.CrashReport.Server.Models.Database;
using BUTR.CrashReport.Server.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReport.Server.Extensions;

/// <summary>
/// Seeds the compression dictionaries baked into this assembly as embedded <c>.bin</c> resources
/// (<c>Migrations/Dictionaries/dict.t{tenant}.{json|html}.v{version}.bin</c>) as the active dictionaries, so new uploads
/// compress with a dictionary from the first request. Runs as a post-migrate deploy step rather than inside a migration:
/// the dictionaries are multi-megabyte blobs and inlining them as hex literals in migration SQL flooded the deploy log
/// (EF logs the full command text). Here the bytes travel as a redacted parameter via EF <c>SaveChanges</c>.
/// Idempotent: a key (tenant/kind/version) already present is skipped, so it is safe to run on every deploy.
/// </summary>
public static class DictionarySeeder
{
    // Embedded resource name -> ...Migrations.Dictionaries.dict.t{tenant}.{json|html}.v{version}.bin
    private static readonly Regex DictName = new(@"\.Migrations\.Dictionaries\.dict\.t(\d+)\.(json|html)\.v(\d+)\.bin$", RegexOptions.Compiled);

    /// <summary>Inserts any embedded dictionary not yet present. Returns a process exit code (0 success, 1 failure).</summary>
    public static async Task<int> SeedEmbeddedDictionariesAsync(this IHost host, CancellationToken ct = default)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var dictionaries = scope.ServiceProvider.GetRequiredService<DictionaryService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DictionaryService>>();

        var assembly = typeof(DictionarySeeder).Assembly;
        var seeded = 0;
        var skipped = 0;
        try
        {
            foreach (var resource in assembly.GetManifestResourceNames())
            {
                var match = DictName.Match(resource);
                if (!match.Success) continue;

                var tenant = byte.Parse(match.Groups[1].Value);
                var kind = match.Groups[2].Value == "html" ? CompressionDictionaryKind.Html : CompressionDictionaryKind.Json;
                var version = byte.Parse(match.Groups[3].Value);

                if (await dictionaries.ExistsAsync(tenant, kind, version, ct))
                {
                    skipped++;
                    continue;
                }

                await using var stream = assembly.GetManifestResourceStream(resource)!;
                using var ms = new System.IO.MemoryStream();
                await stream.CopyToAsync(ms, ct);

                var result = await dictionaries.SetActiveAsync(tenant, kind, version, ms.ToArray(), ct);
                seeded++;
                logger.LogInformation("Seeded compression dictionary id {Id} (tenant {Tenant}, kind {Kind}, version {Version}, {Size} bytes)",
                    result.Id, tenant, kind, version, result.SizeBytes);
            }

            logger.LogInformation("Dictionary seeding complete: {Seeded} seeded, {Skipped} already present", seeded, skipped);
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed embedded compression dictionaries");
            return 1;
        }
    }
}
