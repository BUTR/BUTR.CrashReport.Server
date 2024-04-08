using BUTR.CrashReport.Bannerlord.Parser;
using BUTR.CrashReport.Models;
using BUTR.CrashReport.Renderer.Html;
using BUTR.CrashReportServer.Contexts;
using BUTR.CrashReportServer.Extensions;
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
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReportServer.Controllers;

[ApiController]
[Route("/services")]
public class CrashUploadController : ControllerBase
{
    public sealed record CrashReportUploadBody(CrashReportModel CrashReport, ICollection<LogSource> LogSources);

    private static readonly JsonSerializerOptions _jsonSerializerOptionsWeb = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

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
        var valid = CrashReportParser.TryParse(html, out var version, out var crashReportModel, out _);
        if (!valid)
            return StatusCode(StatusCodes.Status500InternalServerError);

        if (await _dbContext.IdEntities.FirstOrDefaultAsync(x => x.CrashReportId == crashReportModel.Id, ct) is { } idEntity)
            return Ok($"{_options.BaseUri}/{idEntity.FileId}");

        var json = JsonSerializer.Serialize(crashReportModel, _jsonSerializerOptionsWeb);
        await using var compressedHtmlStream = await _gZipCompressor.CompressAsync(Request.Body, ct);

        idEntity = new IdEntity { FileId = GenerateFileId(ct), CrashReportId = crashReportModel.Id, Version = version, Created = DateTime.UtcNow, };
        await _dbContext.IdEntities.AddAsync(idEntity, ct);
        await _dbContext.FileEntities.AddAsync(new FileEntity { FileId = idEntity.FileId, DataCompressed = compressedHtmlStream.ToArray(), }, ct);
        if (version >= 13) await _dbContext.JsonEntities.AddAsync(new JsonEntity { FileId = idEntity.FileId, CrashReport = json, }, ct);
        await _dbContext.SaveChangesAsync(ct);

        _reportVersion.Add(1, new[] { new KeyValuePair<string, object?>("Version", version) });

        return Ok($"{_options.BaseUri}/{idEntity.FileId}");
    }

    private async Task<IActionResult> UploadJsonAsync(CancellationToken ct)
    {
        if (Request.Headers.ContentEncoding.Any(x => x?.Equals("gzip,deflate", StringComparison.OrdinalIgnoreCase) == true))
            Request.Body = await _gZipCompressor.DecompressAsync(Request.Body, ct);

        if (await HttpContext.Request.ReadFromJsonAsync<CrashReportUploadBody>(_jsonSerializerOptions, ct) is not { CrashReport: { } crashReport, LogSources: { } logSources })
            return StatusCode(StatusCodes.Status500InternalServerError);

        if (await _dbContext.IdEntities.FirstOrDefaultAsync(x => x.CrashReportId == crashReport.Id, ct) is { } idEntity)
            return Ok($"{_options.BaseUri}/{idEntity.FileId}");

        var json = JsonSerializer.Serialize(crashReport, _jsonSerializerOptionsWeb);
        var html = CrashReportHtml.AddData(CrashReportHtml.Build(crashReport, logSources), await CompressJson(json));

        await using var compressedHtmlStream = await _gZipCompressor.CompressAsync(html.AsStream(), ct);

        idEntity = new IdEntity { FileId = GenerateFileId(ct), CrashReportId = crashReport.Id, Version = crashReport.Version, Created = DateTime.UtcNow, };
        await _dbContext.IdEntities.AddAsync(idEntity, ct);
        await _dbContext.JsonEntities.AddAsync(new JsonEntity { FileId = idEntity.FileId, CrashReport = json, }, ct);
        await _dbContext.FileEntities.AddAsync(new FileEntity { FileId = idEntity.FileId, DataCompressed = compressedHtmlStream.ToArray(), }, ct);
        await _dbContext.SaveChangesAsync(ct);

        _reportVersion.Add(1, new[] { new KeyValuePair<string, object?>("Version", crashReport.Version) });

        return Ok($"{_options.BaseUri}/{idEntity.FileId}");
    }

    private static async Task<string> CompressJson(string jsonModel)
    {
        using var compressedBase64Stream = new MemoryStream();

        await using (var base64Stream = new CryptoStream(compressedBase64Stream, new ToBase64Transform(), CryptoStreamMode.Write, true))
        await using (var compressorStream = new GZipStream(base64Stream, CompressionLevel.Optimal, true))
        await using (var streamWriter = new StreamWriter(compressorStream, Encoding.UTF8, 1024, true))
        {
            await streamWriter.WriteAsync(jsonModel);
        }

        using (var streamReader = new StreamReader(compressedBase64Stream))
        {
            compressedBase64Stream.Seek(0, SeekOrigin.Begin);
            return await streamReader.ReadToEndAsync();
        }
    }

    [AllowAnonymous]
    [HttpPost("crash-upload.py")]
    [Consumes(typeof(CrashReportUploadBody), "application/json", "text/html")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK, "text/plain")]
    [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public Task<IActionResult> CrashUploadAsync(CancellationToken ct)
    {
        if (Request.ContentLength is not { } contentLength || contentLength < _options.MinContentLength || contentLength > _options.MaxContentLength)
            return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status500InternalServerError));

        switch (Request.ContentType)
        {
            case "application/json":
                return UploadJsonAsync(ct);
            case "text/html":
            default:
                return UploadHtmlAsync(ct);
        }
    }
}