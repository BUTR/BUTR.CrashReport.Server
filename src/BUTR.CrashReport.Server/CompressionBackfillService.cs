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
        "e.data_compressed IS NULL AND e.data IS NOT NULL", "e.data::text", IsJson: true);

    private static readonly BackfillTarget HtmlTarget = new(
        "html", "html_entity", CompressionDictionaryKind.Html,
        "get_byte(e.data_compressed, 0) = 31 AND get_byte(e.data_compressed, 1) = 139 " +
        "AND (r.version >= 13 OR EXISTS (SELECT 1 FROM old_html_entity o WHERE o.crash_report_id = e.crash_report_id))",
        "e.data_compressed", IsJson: false);

    private async Task<long> BackfillAsync(NpgsqlConnection connection, BackfillTarget target, CompressionOptions options, CancellationToken ct)
    {
        var selectSql =
            $"""
             SELECT e.crash_report_id, r.tenant, r.version, {target.PayloadCol}
             FROM {target.Table} e
             JOIN report_entity r ON r.crash_report_id = e.crash_report_id
             JOIN compression_dictionary d ON d.tenant = r.tenant AND d.kind = {(int) target.Kind}
                  AND d.version = (CASE WHEN r.version <= {ZstdCompressionService.LegacyMaxVersion} THEN {ZstdCompressionService.LegacyMaxVersion} ELSE r.version END)
                  AND d.is_active
             WHERE r.version <= {ZstdCompressionService.LegacyMaxVersion} AND {target.NeedsWork}
             ORDER BY e.crash_report_id
             LIMIT @n
             """;
        var updateSql = $"UPDATE {target.Table} SET data_compressed = @c, dict_id = @d WHERE crash_report_id = @id";

        long done = 0;
        while (!ct.IsCancellationRequested)
        {
            var batch = new List<(Guid Id, byte Tenant, byte Version, byte[] Raw)>(options.BackfillBatchSize);
            await using (var select = new NpgsqlCommand(selectSql, connection))
            {
                select.Parameters.AddWithValue("n", options.BackfillBatchSize);
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

            await using var tx = await connection.BeginTransactionAsync(ct);
            await using (var update = new NpgsqlCommand(updateSql, connection, (NpgsqlTransaction) tx))
            {
                var pC = update.Parameters.Add(new NpgsqlParameter("c", NpgsqlTypes.NpgsqlDbType.Bytea));
                var pD = update.Parameters.Add(new NpgsqlParameter("d", NpgsqlTypes.NpgsqlDbType.Smallint));
                var pId = update.Parameters.Add(new NpgsqlParameter("id", NpgsqlTypes.NpgsqlDbType.Uuid));
                await update.PrepareAsync(ct);
                foreach (var (id, tenant, version, raw) in batch)
                {
                    var (compressed, dictId) = await _zstd.CompressAsync(raw, tenant, target.Kind, version, ct);
                    pC.Value = compressed;
                    pD.Value = dictId;
                    pId.Value = id;
                    await update.ExecuteNonQueryAsync(ct);
                    done++;
                }
            }
            await tx.CommitAsync(ct);

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