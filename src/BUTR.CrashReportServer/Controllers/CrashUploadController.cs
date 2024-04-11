using BUTR.CrashReportServer.Contexts;
using BUTR.CrashReportServer.Models.Database;
using BUTR.CrashReportServer.Options;
using BUTR.CrashReportServer.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReportServer.Controllers;

[ApiController]
[Route("/services")]
public partial class CrashUploadController : ControllerBase
{
    private readonly ILogger _logger;
    private readonly CrashUploadOptions _options;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly AppDbContext _dbContext;
    private readonly GZipCompressor _gZipCompressor;
    private readonly HexGenerator _hexGenerator;

    private readonly Counter<int> _reportVersion;

    public CrashUploadController(
        ILogger<CrashUploadController> logger,
        IOptionsSnapshot<CrashUploadOptions> options,
        IOptionsSnapshot<JsonSerializerOptions> jsonSerializerOptions,
        AppDbContext dbContext,
        GZipCompressor gZipCompressor,
        HexGenerator hexGenerator,
        IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("BUTR.CrashReportServer.Controllers.CrashUploadController", "1.0.0");

        _reportVersion = meter.CreateCounter<int>("report-version", unit: "Count");

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonSerializerOptions = jsonSerializerOptions.Value ?? throw new ArgumentNullException(nameof(jsonSerializerOptions));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _gZipCompressor = gZipCompressor ?? throw new ArgumentNullException(nameof(gZipCompressor));
        _hexGenerator = hexGenerator ?? throw new ArgumentNullException(nameof(hexGenerator));
    }

    private string GenerateFileId(CancellationToken ct)
    {
        const int count = 300;
        var fileId = string.Empty;
        while (!ct.IsCancellationRequested)
        {
            var fileIds = _hexGenerator.GetHex(count, 3);
            var existing = _dbContext.IdEntities.Select(x => x.FileId).Where(x => fileIds.Contains(x)).ToHashSet();
            if (existing.Count == fileIds.Count) continue;
            if (existing.Count == 0) return fileIds.First();
            return fileIds.First(x => !existing.Contains(x));
        }
        return fileId;
    }

    private async Task<IActionResult> UploadHtmlAsync(CancellationToken ct)
    {
        Request.EnableBuffering();

        using var streamReader = new StreamReader(Request.Body);
        var html = await streamReader.ReadToEndAsync(ct);
        var (valid, version, crashReportModel) = ParseHtmlV13(html);
        if (!valid)
            return StatusCode(StatusCodes.Status500InternalServerError);

        if (await _dbContext.IdEntities.FirstOrDefaultAsync(x => x.CrashReportId == crashReportModel!.Id, ct) is { } idEntity)
            return Ok($"{_options.BaseUri}/{idEntity.FileId}");

        var json = JsonSerializer.Serialize(crashReportModel, _jsonSerializerOptionsWeb);
        await using var compressedHtmlStream = await _gZipCompressor.CompressAsync(Request.Body, ct);

        idEntity = new IdEntity { FileId = GenerateFileId(ct), CrashReportId = crashReportModel!.Id, Version = version, Created = DateTime.UtcNow, };
        await _dbContext.IdEntities.AddAsync(idEntity, ct);
        await _dbContext.FileEntities.AddAsync(new FileEntity { FileId = idEntity.FileId, DataCompressed = compressedHtmlStream.ToArray(), }, ct);
        if (version >= 13) await _dbContext.JsonEntities.AddAsync(new JsonEntity { FileId = idEntity.FileId, CrashReport = json, }, ct);
        await _dbContext.SaveChangesAsync(ct);

        _reportVersion.Add(1, new[] { new KeyValuePair<string, object?>("Version", version) });

        return Ok($"{_options.BaseUri}/{idEntity.FileId}");
    }

    [AllowAnonymous]
    [HttpPost("crash-upload.py")]
    [Consumes(typeof(CrashReportUploadBodyV13), "application/json", "text/html")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK, "text/plain")]
    [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public Task<IActionResult> CrashUploadAsync(CancellationToken ct)
    {
        if (Request.ContentLength is not { } contentLength || contentLength < _options.MinContentLength || contentLength > _options.MaxContentLength)
            return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status500InternalServerError));

        switch (Request.ContentType)
        {
            case "application/json":
            {
                switch (Request.Headers["CrashReportVersion"])
                {
                    case "13":
                    default:
                        return UploadJsonV13Async(ct);
                }
            }
            case "text/html":
            default:
            {
                return UploadHtmlAsync(ct);
            }
        }
    }
}