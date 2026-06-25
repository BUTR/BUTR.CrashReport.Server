using BUTR.CrashReport.Server.Contexts;
using BUTR.CrashReport.Server.Extensions;
using BUTR.CrashReport.Server.Models.Database;
using BUTR.CrashReport.Server.v13;

using Microsoft.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReport.Server.Services;

/// <summary>
/// One-off maintenance: re-renders legacy (&lt;13) reports whose stored HTML is the original verbatim upload. For each
/// report it archives the original HTML into <see cref="OldHtmlEntity"/>, replaces the live <see cref="HtmlEntity"/>
/// with the trusted renderer's output (so any injected markup is stripped), and stores the parsed model as JSON.
/// HTML is still streamed straight from <see cref="HtmlEntity"/> on read; this only sanitizes its contents.
/// <para>
/// Reports whose HTML can't be parsed are left untouched (no archive, no re-render) so nothing is lost and a later
/// run can retry them.
/// </para>
/// <para>Idempotent: a report is considered migrated once it has an <see cref="OldHtmlEntity"/> row, so re-runs skip
/// it. Pass a <c>limit</c> to bound the work per call and drain the backlog in chunks: call until <c>Migrated</c> is
/// 0 (a residual <c>Remaining</c> then is reports whose HTML cannot be parsed and need manual attention).</para>
/// </summary>
public sealed class LegacyHtmlToJsonMigrator
{
    public sealed record MigrationResult(int Processed, int Migrated, int Skipped, int Remaining);

    private const int BatchSize = 100;

    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly GZipCompressor _gZipCompressor;

    public LegacyHtmlToJsonMigrator(IDbContextFactory<AppDbContext> dbContextFactory, GZipCompressor gZipCompressor)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _gZipCompressor = gZipCompressor ?? throw new ArgumentNullException(nameof(gZipCompressor));
    }

    // Legacy reports whose original HTML hasn't been archived yet. The OldHtmlEntity row is the migration marker,
    // so re-runs skip everything already re-rendered without relying on deleting anything.
    private static IQueryable<Guid> Candidates(AppDbContext dbContext) => dbContext.HtmlEntities
        .Where(x => x.Report!.Version < 13)
        .Where(x => !dbContext.OldHtmlEntities.Any(o => o.CrashReportId == x.CrashReportId))
        .Select(x => x.CrashReportId);

    /// <param name="limit">Maximum number of reports to re-render this call; null processes the whole backlog at once.</param>
    public async Task<MigrationResult> MigrateAsync(int? limit, CancellationToken ct)
    {
        List<Guid> ids;
        await using (var dbContext = await _dbContextFactory.CreateDbContextAsync(ct))
        {
            var query = Candidates(dbContext).OrderBy(x => x);
            ids = limit is { } l
                ? await query.Take(Math.Max(0, l)).ToListAsync(ct)
                : await query.ToListAsync(ct);
        }

        var processed = 0;
        var migrated = 0;
        var skipped = 0;

        for (var offset = 0; offset < ids.Count; offset += BatchSize)
        {
            var slice = ids.GetRange(offset, Math.Min(BatchSize, ids.Count - offset));

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
            var htmlEntities = await dbContext.HtmlEntities.Where(x => slice.Contains(x.CrashReportId)).ToListAsync(ct);
            foreach (var htmlEntity in htmlEntities)
            {
                processed++;

                var originalCompressed = htmlEntity.DataCompressed;
                string original;
                await using (var decompressed = await _gZipCompressor.DecompressAsync(originalCompressed, ct))
                using (var reader = new StreamReader(decompressed))
                    original = await reader.ReadToEndAsync(ct);

                // Leave anything that no longer parses as a legacy report alone - no archive row is written, so it
                // stays a candidate and a later run can retry it.
                if (HtmlHandlerV13.Rebuild(original) is not { } rebuilt)
                {
                    skipped++;
                    continue;
                }

                // Archive the original, replace the live HTML with the rendered output, and store the model JSON.
                await dbContext.OldHtmlEntities.AddAsync(new OldHtmlEntity { CrashReportId = htmlEntity.CrashReportId, DataCompressed = originalCompressed, }, ct);
                await using (var compressed = await _gZipCompressor.CompressAsync(rebuilt.Html.AsStream(), ct))
                    htmlEntity.DataCompressed = compressed.ToArray();
                await dbContext.JsonEntities.AddAsync(new JsonEntity { CrashReportId = htmlEntity.CrashReportId, Json = rebuilt.Json, }, ct);
                migrated++;
            }
            await dbContext.SaveChangesAsync(ct);
        }

        int remaining;
        await using (var dbContext = await _dbContextFactory.CreateDbContextAsync(ct))
            remaining = await Candidates(dbContext).CountAsync(ct);

        return new MigrationResult(processed, migrated, skipped, remaining);
    }
}
