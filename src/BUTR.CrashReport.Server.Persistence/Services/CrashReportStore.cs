using BUTR.CrashReport.Server.Contexts;
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
        var deleteToken = DeleteTokenGenerator.Generate();
        var deleteTokenHash = DeleteTokenGenerator.ComputeHash(deleteToken);
        var fileIds = _fileIdGenerator.Generate();
        var created = DateTime.UtcNow;

        byte[]? htmlCompressed = null;
        if (htmlStream is not null)
        {
            await using var compressedHtmlStream = await _gZipCompressor.CompressAsync(htmlStream, ct);
            htmlCompressed = compressedHtmlStream.ToArray();
        }

        var result = await _dbContext
            .InsertCrashReport(crashReportId, tenant, version, created, deleteTokenHash, fileIds, htmlCompressed, json)
            .SingleAsync(ct);

        if (result.Created)
        {
            _reportTenant.Add(1, new KeyValuePair<string, object?>("Tenant", tenant));
            _reportVersion.Add(1, new KeyValuePair<string, object?>("Version", version));
        }

        var url = BuildUrl(result.Tenant, result.FileId);
        // Only the original uploader gets a delete token; a re-upload (Created = false) returns the URL without one.
        return new StoreResult(url, result.Created ? $"{url}?{DeleteTokenGenerator.QueryName}={deleteToken}" : null);
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
