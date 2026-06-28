using BUTR.CrashReport.Server.Contexts;
using BUTR.CrashReport.Server.Models.Database;
using BUTR.CrashReport.Server.Options;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReport.Server.Services;

public sealed class CrashReportStore
{
    public sealed record StoreResult(string Url, string? DeleteUrl);

    private readonly AppDbContext _dbContext;
    private readonly ZstdCompressionService _zstd;
    private readonly FileIdGenerator _fileIdGenerator;
    private readonly Counter<int> _reportTenant;
    private readonly Counter<int> _reportVersion;
    private CrashUploadOptions _options;

    public CrashReportStore(
        IOptionsMonitor<CrashUploadOptions> options,
        AppDbContext dbContext,
        ZstdCompressionService zstd,
        FileIdGenerator fileIdGenerator,
        IMeterFactory meterFactory)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _zstd = zstd ?? throw new ArgumentNullException(nameof(zstd));
        _fileIdGenerator = fileIdGenerator ?? throw new ArgumentNullException(nameof(fileIdGenerator));

        var meter = meterFactory.Create("BUTR.CrashReportServer.Controllers.CrashUploadController", "1.0.0");
        _reportTenant = meter.CreateCounter<int>("report-tenant", unit: "Count");
        _reportVersion = meter.CreateCounter<int>("report-version", unit: "Count");

        _options = options.CurrentValue ?? throw new ArgumentNullException(nameof(options));
        options.OnChange(x => _options = x);
    }

    public async Task<StoreResult> StoreAsync(Guid crashReportId, byte tenant, byte version, Stream? htmlStream, string? json, CancellationToken ct)
    {
        var deleteToken = DeleteTokenGenerator.Generate();
        var deleteTokenHash = DeleteTokenGenerator.ComputeHash(deleteToken);
        var fileIds = _fileIdGenerator.Generate();
        var created = DateTime.UtcNow;

        byte[]? htmlCompressed = null;
        short? htmlDictId = null;
        if (htmlStream is not null)
        {
            using var htmlBuffer = new MemoryStream();
            await htmlStream.CopyToAsync(htmlBuffer, ct);
            (htmlCompressed, htmlDictId) = await _zstd.CompressAsync(htmlBuffer.ToArray(), tenant, CompressionDictionaryKind.Html, version, ct);
        }

        byte[]? jsonCompressed = null;
        short? jsonDictId = null;
        if (json is not null)
            (jsonCompressed, jsonDictId) = await _zstd.CompressAsync(Encoding.UTF8.GetBytes(json), tenant, CompressionDictionaryKind.Json, version, ct);

        var result = await _dbContext
            .InsertCrashReport(crashReportId, tenant, version, created, deleteTokenHash, fileIds, htmlCompressed, htmlDictId, jsonCompressed, jsonDictId)
            .SingleAsync(ct);

        if (result.Created)
        {
            _reportTenant.Add(1, new KeyValuePair<string, object?>("Tenant", tenant));
            _reportVersion.Add(1, new KeyValuePair<string, object?>("Version", version));
        }

        var url = BuildUrl(result.Tenant, result.FileId);
        return new StoreResult(url, result.Created ? $"{url}?{DeleteTokenGenerator.QueryName}={deleteToken}" : null);
    }

    private string BuildUrl(byte tenant, string fileId) => tenant == 1
        ? $"{_options.BaseUri}/{fileId}"
        : $"{_options.BaseUri}/{tenant}/{fileId}";
}

public static class CrashReportStoreExtensions
{
    public static IActionResult Respond(this CrashReportStore.StoreResult result, ControllerBase controller)
    {
        if (result.DeleteUrl is { } deleteUrl)
            controller.Response.Headers[DeleteTokenGenerator.HeaderName] = deleteUrl;
        return controller.Ok(result.Url);
    }
}