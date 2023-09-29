using BUTR.CrashReport.Bannerlord;
using BUTR.CrashReport.Models;
using BUTR.CrashReportServer.Contexts;
using BUTR.CrashReportServer.Extensions;
using BUTR.CrashReportServer.Models.Database;
using BUTR.CrashReportServer.Options;
using BUTR.CrashReportServer.Services;
using BUTR.CrashReportServer.Utils;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
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

    private readonly ILogger _logger;
    private readonly CrashUploadOptions _options;
    private readonly AppDbContext _dbContext;
    private readonly GZipCompressor _gZipCompressor;

    public CrashUploadController(ILogger<CrashUploadController> logger, IOptionsSnapshot<CrashUploadOptions> options, AppDbContext dbContext, GZipCompressor gZipCompressor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _gZipCompressor = gZipCompressor ?? throw new ArgumentNullException(nameof(gZipCompressor));
    }

    /*
    private async Task<string> GenerateFileId(CancellationToken ct)
    {
        const int count = 100;
        var fileId = string.Empty;
        while (!ct.IsCancellationRequested)
        {
            var fileIds = Enumerable.Range(0, count).Select(_ => _hexGenerator.GetHex()).ToHashSet();
            var existing = await _dbContext.Set<IdEntity>().Select(x => x.FileId).Where(x => fileIds.Contains(x)).ToArrayAsync(ct);
            if (existing.Length == count) continue;
            fileIds.ExceptWith(existing);
            fileId = fileIds.First();
            break;
        }
        return fileId;
    }
    */

    [AllowAnonymous]
    [HttpPost("crash-upload.py")]
    [Consumes("text/html")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK, "text/plain")]
    [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> CrashUploadAsync(CancellationToken ct)
    {
        if (Request.ContentLength is not { } contentLength || contentLength < _options.MinContentLength || contentLength > _options.MaxContentLength)
            return StatusCode((int) HttpStatusCode.InternalServerError);

        Request.EnableBuffering();

        var (valid, id, version, crashReportModel) = await CrashReportRawParser.TryReadCrashReportDataAsync(PipeReader.Create(Request.Body));
        if (!valid)
            return StatusCode((int) HttpStatusCode.InternalServerError);

        if (await _dbContext.Set<IdEntity>().FirstOrDefaultAsync(x => x.CrashReportId == id, ct) is { } idEntity)
            return Ok($"{_options.BaseUri}/{idEntity.FileId}");

        await using var compressedHtmlStream = await _gZipCompressor.CompressAsync(Request.Body, ct);
        await using var compressedJsonStream = await _gZipCompressor.CompressAsync(JsonSerializer.SerializeToUtf8Bytes(crashReportModel, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        }), ct);

        idEntity = new IdEntity { FileId = default!, CrashReportId = id, Created = DateTimeOffset.UtcNow, };
        await _dbContext.Set<IdEntity>().AddAsync(idEntity, ct);
        await _dbContext.Set<FileEntity>().AddAsync(new FileEntity { Id = idEntity, DataCompressed = compressedHtmlStream.ToArray(), }, ct);
        if (version >= 13) await _dbContext.Set<JsonEntity>().AddAsync(new JsonEntity { Id = idEntity, CrashReportCompressed = compressedJsonStream.ToArray(), }, ct);
        await _dbContext.SaveChangesAsync(ct);

        return Ok($"{_options.BaseUri}/{idEntity.FileId}");
    }

    [AllowAnonymous]
    [HttpPost("crashupload")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK, "text/plain")]
    [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> CrashUploadAsync([FromBody] CrashReportUploadBody body, CancellationToken ct)
    {
        if (Request.ContentLength is not { } contentLength || contentLength < _options.MinContentLength || contentLength > _options.MaxContentLength)
            return StatusCode((int) HttpStatusCode.InternalServerError);

        if (await _dbContext.Set<IdEntity>().FirstOrDefaultAsync(x => x.CrashReportId == body.CrashReport.Id, ct) is { } idEntity)
            return Ok($"{_options.BaseUri}/{idEntity.FileId}");

        var json = JsonSerializer.Serialize(body.CrashReport, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        });
        var html = CrashReportHtmlRenderer.AddData(CrashReportHtmlRenderer.Build(body.CrashReport, body.LogSources), json);

        await using var compressedHtmlStream = await _gZipCompressor.CompressAsync(html.AsStream(), ct);
        await using var compressedJsonStream = await _gZipCompressor.CompressAsync(json.AsStream(), ct);

        idEntity = new IdEntity { FileId = default!, CrashReportId = body.CrashReport.Id, Created = DateTimeOffset.UtcNow, };
        await _dbContext.Set<IdEntity>().AddAsync(idEntity, ct);
        await _dbContext.Set<JsonEntity>().AddAsync(new JsonEntity { Id = idEntity, CrashReportCompressed = compressedJsonStream.ToArray(), }, ct);
        await _dbContext.Set<FileEntity>().AddAsync(new FileEntity { Id = idEntity, DataCompressed = compressedHtmlStream.ToArray(), }, ct);
        await _dbContext.SaveChangesAsync(ct);

        return Ok($"{_options.BaseUri}/{idEntity.FileId}");
    }
}