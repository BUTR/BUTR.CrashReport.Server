using BUTR.CrashReport.Server.Contexts;
using BUTR.CrashReport.Server.Models.Database;
using BUTR.CrashReport.Server.Options;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReport.Server.Services;

/// <summary>
/// Persists an uploaded crash report and produces the public report URL plus a one-time delete URL.
/// Shared by every versioned upload handler so the storage logic lives in a single place.
/// </summary>
public sealed class CrashReportStore
{
    /// <param name="Url">The public report URL.</param>
    /// <param name="DeleteUrl">The report URL with the delete token appended, or null when the report already existed.</param>
    public sealed record StoreResult(string Url, string? DeleteUrl);

    private readonly AppDbContext _dbContext;
    private readonly GZipCompressor _gZipCompressor;
    private readonly FileIdGenerator _fileIdGenerator;
    private readonly Counter<int> _reportTenant;
    private readonly Counter<int> _reportVersion;
    private CrashUploadOptions _options;

    public CrashReportStore(
        IOptionsMonitor<CrashUploadOptions> options,
        AppDbContext dbContext,
        GZipCompressor gZipCompressor,
        FileIdGenerator fileIdGenerator,
        IMeterFactory meterFactory)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _gZipCompressor = gZipCompressor ?? throw new ArgumentNullException(nameof(gZipCompressor));
        _fileIdGenerator = fileIdGenerator ?? throw new ArgumentNullException(nameof(fileIdGenerator));

        var meter = meterFactory.Create("BUTR.CrashReportServer.Controllers.CrashUploadController", "1.0.0");
        _reportTenant = meter.CreateCounter<int>("report-tenant", unit: "Count");
        _reportVersion = meter.CreateCounter<int>("report-version", unit: "Count");

        _options = options.CurrentValue ?? throw new ArgumentNullException(nameof(options));
        options.OnChange(x => _options = x);
    }

    /// <summary>
    /// Stores a crash report. If a report with the same <paramref name="crashReportId"/> already exists, its URL is
    /// returned without a new delete token. Otherwise the report is persisted and a fresh delete token is issued.
    /// </summary>
    /// <param name="htmlStream">The pre-rendered HTML to store, or null to store no HTML (legacy reports are rendered on demand from <paramref name="json"/>).</param>
    /// <param name="json">The JSON representation to store, or null to store no JSON.</param>
    public async Task<StoreResult> StoreAsync(Guid crashReportId, byte tenant, byte version, Stream? htmlStream, string? json, CancellationToken ct)
    {
        // The same crash report was already uploaded - return its existing URL, but no delete token (only the original uploader holds it).
        if (await _dbContext.IdEntities.AsNoTracking().Include(x => x.Report).FirstOrDefaultAsync(x => x.CrashReportId == crashReportId, ct) is { } existing)
            return new StoreResult(BuildUrl(existing.Report?.Tenant ?? 1, existing.FileId), null);

        var fileId = _fileIdGenerator.Generate(ct);
        var deleteToken = DeleteTokenGenerator.Generate();

        await _dbContext.ReportEntities.AddAsync(new ReportEntity
        {
            CrashReportId = crashReportId,
            Tenant = tenant,
            Version = version,
            Created = DateTime.UtcNow,
            DeleteTokenHash = DeleteTokenGenerator.ComputeHash(deleteToken),
        }, ct);
        await _dbContext.IdEntities.AddAsync(new IdEntity { CrashReportId = crashReportId, FileId = fileId, }, ct);
        if (htmlStream is not null)
        {
            await using var compressedHtmlStream = await _gZipCompressor.CompressAsync(htmlStream, ct);
            await _dbContext.HtmlEntities.AddAsync(new HtmlEntity { CrashReportId = crashReportId, DataCompressed = compressedHtmlStream.ToArray(), }, ct);
        }
        if (json is not null) await _dbContext.JsonEntities.AddAsync(new JsonEntity { CrashReportId = crashReportId, Json = json, }, ct);
        await _dbContext.SaveChangesAsync(ct);

        _reportTenant.Add(1, new KeyValuePair<string, object?>("Tenant", tenant));
        _reportVersion.Add(1, new KeyValuePair<string, object?>("Version", version));

        var url = BuildUrl(tenant, fileId);
        return new StoreResult(url, $"{url}?{DeleteTokenGenerator.QueryName}={deleteToken}");
    }

    private string BuildUrl(byte tenant, string fileId) => tenant == 1
        ? $"{_options.BaseUri}/{fileId}"
        : $"{_options.BaseUri}/{tenant}/{fileId}";
}

public static class CrashReportStoreExtensions
{
    /// <summary>
    /// Returns the report URL as a 200 response, exposing the one-time delete URL via the
    /// <see cref="DeleteTokenGenerator.HeaderName"/> header when a fresh token was issued.
    /// </summary>
    public static IActionResult Respond(this CrashReportStore.StoreResult result, ControllerBase controller)
    {
        if (result.DeleteUrl is { } deleteUrl)
            controller.Response.Headers[DeleteTokenGenerator.HeaderName] = deleteUrl;
        return controller.Ok(result.Url);
    }
}
