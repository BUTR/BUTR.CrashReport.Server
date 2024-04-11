extern alias v13;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using BUTR.CrashReportServer.Extensions;
using BUTR.CrashReportServer.Models.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BUTR.CrashReportServer.Controllers;

partial class CrashUploadController
{
    private sealed record CrashReportUploadBodyV13(
        v13::BUTR.CrashReport.Models.CrashReportModel CrashReport,
        ICollection<v13::BUTR.CrashReport.Models.LogSource> LogSources);

    private static readonly JsonSerializerOptions _jsonSerializerOptionsWeb = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private async Task<IActionResult> UploadJsonV13Async(CancellationToken ct)
    {
        if (Request.Headers.ContentEncoding.Any(x => x?.Equals("gzip,deflate", StringComparison.OrdinalIgnoreCase) == true))
            Request.Body = await _gZipCompressor.DecompressAsync(Request.Body, ct);

        if (await HttpContext.Request.ReadFromJsonAsync<CrashReportUploadBodyV13>(_jsonSerializerOptions, ct) is not { CrashReport: { } crashReport, LogSources: { } logSources })
            return StatusCode(StatusCodes.Status500InternalServerError);

        if (await _dbContext.IdEntities.FirstOrDefaultAsync(x => x.CrashReportId == crashReport.Id, ct) is { } idEntity)
            return Ok($"{_options.BaseUri}/{idEntity.FileId}");

        var json = JsonSerializer.Serialize(crashReport, _jsonSerializerOptionsWeb);
        var html = v13::BUTR.CrashReport.Renderer.Html.CrashReportHtml.Build(crashReport, logSources);

        await using var compressedHtmlStream = await _gZipCompressor.CompressAsync(html.AsStream(), ct);

        idEntity = new IdEntity { FileId = GenerateFileId(ct), CrashReportId = crashReport.Id, Version = crashReport.Version, Created = DateTime.UtcNow, };
        await _dbContext.IdEntities.AddAsync(idEntity, ct);
        await _dbContext.JsonEntities.AddAsync(new JsonEntity { FileId = idEntity.FileId, CrashReport = json, }, ct);
        await _dbContext.FileEntities.AddAsync(new FileEntity { FileId = idEntity.FileId, DataCompressed = compressedHtmlStream.ToArray(), }, ct);
        await _dbContext.SaveChangesAsync(ct);

        _reportVersion.Add(1, new[] { new KeyValuePair<string, object?>("Version", crashReport.Version) });

        return Ok($"{_options.BaseUri}/{idEntity.FileId}");
    }
}