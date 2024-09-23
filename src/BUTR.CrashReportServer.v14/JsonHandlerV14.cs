using BUTR.CrashReport.Renderer.Html;
using BUTR.CrashReportServer.Contexts;
using BUTR.CrashReportServer.Extensions;
using BUTR.CrashReportServer.Models.Database;
using BUTR.CrashReportServer.Options;
using BUTR.CrashReportServer.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReportServer.v14;

public class JsonHandlerV14
{
    private static readonly JsonSerializerOptions _jsonSerializerOptionsWeb = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ILogger _logger;
    private readonly AppDbContext _dbContext;
    private readonly GZipCompressor _gZipCompressor;
    private readonly FileIdGenerator _fileIdGenerator;
    private CrashUploadOptions _options;
    private JsonSerializerOptions _jsonSerializerOptions;

    private readonly Counter<int> _reportVersion;

    public JsonHandlerV14(
        ILogger<JsonHandlerV14> logger,
        IOptionsMonitor<CrashUploadOptions> options,
        IOptionsMonitor<JsonSerializerOptions> jsonSerializerOptions,
        AppDbContext dbContext,
        GZipCompressor gZipCompressor,
        FileIdGenerator fileIdGenerator,
        IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("BUTR.CrashReportServer.Controllers.CrashUploadController", "1.0.0");
        _reportVersion = meter.CreateCounter<int>("report-version", unit: "Count");

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _gZipCompressor = gZipCompressor ?? throw new ArgumentNullException(nameof(gZipCompressor));
        _fileIdGenerator = fileIdGenerator;

        _jsonSerializerOptions = jsonSerializerOptions.CurrentValue ?? throw new ArgumentNullException(nameof(jsonSerializerOptions));
        jsonSerializerOptions.OnChange(x => _jsonSerializerOptions = x);
        _options = options.CurrentValue ?? throw new ArgumentNullException(nameof(options));
        options.OnChange(x => _options = x);
    }

    public async Task<IActionResult> UploadJsonAsync(ControllerBase controller, CancellationToken ct)
    {
        if (controller.Request.Headers.ContentEncoding.Any(x => x?.Equals("gzip,deflate", StringComparison.OrdinalIgnoreCase) == true))
            controller.Request.Body = await _gZipCompressor.DecompressAsync(controller.Request.Body, ct);

        if (await controller.HttpContext.Request.ReadFromJsonAsync<CrashReportUploadBodyV14>(_jsonSerializerOptions, ct) is not { CrashReport: { } crashReport, LogSources: { } logSources })
            return controller.StatusCode(StatusCodes.Status500InternalServerError);

        if (await _dbContext.IdEntities.FirstOrDefaultAsync(x => x.CrashReportId == crashReport.Id, ct) is { } idEntity)
            return controller.Ok($"{_options.BaseUri}/{idEntity.FileId}");

        var json = JsonSerializer.Serialize(crashReport, _jsonSerializerOptionsWeb);
        var html = CrashReportHtml.Build(crashReport, logSources);

        await using var compressedHtmlStream = await _gZipCompressor.CompressAsync(html.AsStream(), ct);

        idEntity = new IdEntity { FileId = _fileIdGenerator.Generate(ct), CrashReportId = crashReport.Id, Version = crashReport.Version, Created = DateTime.UtcNow, };
        await _dbContext.IdEntities.AddAsync(idEntity, ct);
        await _dbContext.JsonEntities.AddAsync(new JsonEntity { FileId = idEntity.FileId, CrashReport = json, }, ct);
        await _dbContext.FileEntities.AddAsync(new FileEntity { FileId = idEntity.FileId, DataCompressed = compressedHtmlStream.ToArray(), }, ct);
        await _dbContext.SaveChangesAsync(ct);

        _reportVersion.Add(1, new[] { new KeyValuePair<string, object?>("Version", crashReport.Version) });

        return controller.Ok($"{_options.BaseUri}/{idEntity.FileId}");
    }
}