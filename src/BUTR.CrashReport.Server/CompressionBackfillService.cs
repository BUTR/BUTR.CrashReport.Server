using BUTR.CrashReport.Server.Models.Database;
using BUTR.CrashReport.Server.Options;
using BUTR.CrashReport.Server.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReport.Server;

public sealed class CompressionBackfillService : BackgroundService
{
    // Arbitrary fixed key so concurrent instances/replicas don't backfill at the same time.
    private const long AdvisoryLockKey = 0x42555452_4246; // "BUTRBF"

    private readonly IConfiguration _configuration;
    private readonly ZstdCompressionService _zstd;
    private readonly IOptionsMonitor<CompressionOptions> _options;
    private readonly ILogger<CompressionBackfillService> _logger;

    public CompressionBackfillService(IConfiguration configuration, ZstdCompressionService zstd, IOptionsMonitor<CompressionOptions> options, ILogger<CompressionBackfillService> logger)
    {
        _configuration = configuration;
        _zstd = zstd;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.CurrentValue;
        if (!options.BackfillEnabled)
        {
            _logger.LogInformation("Compression backfill disabled (Compression:BackfillEnabled=false)");
            return;
        }

        var connectionString = _configuration.GetConnectionString("Main");
        if (string.IsNullOrEmpty(connectionString)) return;

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(stoppingToken);

            await using (var lockCmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(@k)", connection))
            {
                lockCmd.Parameters.AddWithValue("k", AdvisoryLockKey);
                if (await lockCmd.ExecuteScalarAsync(stoppingToken) is not true)
                {
                    _logger.LogInformation("Another instance holds the compression backfill lock; skipping");
                    return;
                }
            }

            try
            {
                _logger.LogInformation("Compression backfill started (batch={Batch}, pause={Pause}ms)", options.BackfillBatchSize, options.BackfillPauseMs);
                var json = await BackfillAsync(connection, JsonTarget, options, stoppingToken);
                var html = await BackfillAsync(connection, HtmlTarget, options, stoppingToken);
                _logger.LogInformation("Compression backfill complete (json={Json}, html={Html} rows)", json, html);
            }
            finally
            {
                await using var unlock = new NpgsqlCommand("SELECT pg_advisory_unlock(@k)", connection);
                unlock.Parameters.AddWithValue("k", AdvisoryLockKey);
                await unlock.ExecuteScalarAsync(CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // app shutting down - the session lock releases with the connection
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compression backfill aborted");
        }
    }

    private sealed record BackfillTarget(string Name, string Table, CompressionDictionaryKind Kind, string NeedsWork, string PayloadCol, bool IsJson);

    private static readonly BackfillTarget JsonTarget = new(
        "json", "json_entity", CompressionDictionaryKind.Json,
        "e.dict_id IS NULL AND e.data IS NOT NULL", "e.data::text", IsJson: true);

    private static readonly BackfillTarget HtmlTarget = new(
        "html", "html_entity", CompressionDictionaryKind.Html,
        "e.dict_id IS NULL AND (r.version >= 13 OR EXISTS (SELECT 1 FROM old_html_entity o WHERE o.crash_report_id = e.crash_report_id))",
        "e.data_compressed", IsJson: false);

    private async Task<long> BackfillAsync(NpgsqlConnection connection, BackfillTarget target, CompressionOptions options, CancellationToken ct)
    {
        var selectSql =
            $"""
             SELECT e.crash_report_id, r.tenant, r.version, {target.PayloadCol}
             FROM {target.Table} e
             JOIN report_entity r ON r.crash_report_id = e.crash_report_id
             WHERE r.version <= {ZstdCompressionService.LegacyMaxVersion} AND {target.NeedsWork}
                  AND e.crash_report_id > @after
             ORDER BY e.crash_report_id
             LIMIT @n
             """;
        var updateSql =
            $"""
             UPDATE {target.Table} AS e
             SET data_compressed = u.c, dict_id = u.d
             FROM unnest(@ids, @cs, @ds) AS u(id, c, d)
             WHERE e.crash_report_id = u.id
             """;

        var after = Guid.Empty;
        long done = 0;
        while (!ct.IsCancellationRequested)
        {
            var batch = new List<(Guid Id, byte Tenant, byte Version, byte[] Raw)>(options.BackfillBatchSize);
            await using (var select = new NpgsqlCommand(selectSql, connection))
            {
                select.Parameters.AddWithValue("n", options.BackfillBatchSize);
                select.Parameters.AddWithValue("after", after);
                select.CommandTimeout = 600;
                await using var reader = await select.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var id = reader.GetGuid(0);
                    var tenant = (byte) reader.GetInt16(1);
                    var version = (byte) reader.GetInt16(2);
                    var raw = target.IsJson
                        ? System.Text.Encoding.UTF8.GetBytes(reader.GetString(3))
                        : Gunzip((byte[]) reader[3]);
                    batch.Add((id, tenant, version, raw));
                }
            }

            if (batch.Count == 0) break;

            var ids = new Guid[batch.Count];
            var payloads = new byte[batch.Count][];
            var dictIds = new short[batch.Count];
            await Parallel.ForAsync(0, batch.Count,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct },
                async (i, token) =>
                {
                    var (id, tenant, version, raw) = batch[i];
                    try
                    {
                        var (compressed, dictId) = await _zstd.CompressAsync(raw, tenant, target.Kind, version, token);
                        ids[i] = id;
                        payloads[i] = compressed;
                        dictIds[i] = dictId;
                    }
                    catch (InvalidOperationException ex)
                    {
                        // No active dictionary (and no tenant-1 fallback) for this row - skip it, don't kill the run.
                        _logger.LogWarning(ex, "Compression backfill {Kind}: skipping row {Id} (tenant {Tenant})", target.Name, id, tenant);
                    }
                });

            var okIds = new List<Guid>(batch.Count);
            var okPayloads = new List<byte[]>(batch.Count);
            var okDictIds = new List<short>(batch.Count);
            for (var i = 0; i < batch.Count; i++)
            {
                if (payloads[i] is null) continue;
                okIds.Add(ids[i]);
                okPayloads.Add(payloads[i]);
                okDictIds.Add(dictIds[i]);
            }

            if (okIds.Count > 0)
            {
                await using var tx = await connection.BeginTransactionAsync(ct);
                await using var update = new NpgsqlCommand(updateSql, connection, tx);
                update.Parameters.Add(new NpgsqlParameter("ids", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Uuid) { Value = okIds.ToArray() });
                update.Parameters.Add(new NpgsqlParameter("cs", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Bytea) { Value = okPayloads.ToArray() });
                update.Parameters.Add(new NpgsqlParameter("ds", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Smallint) { Value = okDictIds.ToArray() });
                update.CommandTimeout = 600;
                await update.ExecuteNonQueryAsync(ct);
                await tx.CommitAsync(ct);
            }
            done += okIds.Count;

            // Rows come back ordered by crash_report_id, so the last is the highest id in this batch.
            after = batch[^1].Id;

            _logger.LogInformation("Compression backfill {Kind}: {Done} rows compressed", target.Name, done);
            if (options.BackfillPauseMs > 0) await Task.Delay(options.BackfillPauseMs, ct);
        }
        return done;
    }

    private static byte[] Gunzip(byte[] gz)
    {
        using var src = new MemoryStream(gz);
        using var gzs = new GZipStream(src, CompressionMode.Decompress);
        using var outp = new MemoryStream();
        gzs.CopyTo(outp);
        return outp.ToArray();
    }
}