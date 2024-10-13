using BUTR.CrashReport.Renderer.Html;
using BUTR.CrashReport.Server.Contexts;
using BUTR.CrashReport.Server.Extensions;
using BUTR.CrashReport.Server.Models.Database;
using BUTR.CrashReport.Server.Options;
using BUTR.CrashReport.Server.Services;

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
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReport.Server.v13;

public class JsonHandlerV13
{
    private static readonly JsonSerializerOptions _jsonSerializerOptionsWeb = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ILogger _logger;
    private readonly AppDbContext _dbContext;
    private readonly GZipCompressor _gZipCompressor;
    private readonly FileIdGenerator _fileIdGenerator;
    private JsonSerializerOptions _jsonSerializerOptions;
    private CrashUploadOptions _options;

    private readonly Counter<int> _reportTenant;
    private readonly Counter<int> _reportVersion;

    public JsonHandlerV13(
        ILogger<JsonHandlerV13> logger,
        IOptionsMonitor<CrashUploadOptions> options,
        IOptionsMonitor<JsonSerializerOptions> jsonSerializerOptions,
        AppDbContext dbContext,
        GZipCompressor gZipCompressor,
        FileIdGenerator fileIdGenerator,
        IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("BUTR.CrashReportServer.Controllers.CrashUploadController", "1.0.0");
        _reportTenant = meter.CreateCounter<int>("report-tenant", unit: "Count");
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
        var tenant = byte.TryParse(controller.Request.Headers["Tenant"].ToString(), out var tenantId) ? tenantId : (byte) 1;

        if (controller.Request.Headers.ContentEncoding is ["gzip", "deflate"] or ["gzip,deflate"] or ["gzip, deflate"])
            controller.Request.Body = await _gZipCompressor.DecompressAsync(controller.Request.Body, ct);
        else
            controller.Request.EnableBuffering();

        if (await controller.HttpContext.Request.ReadFromJsonAsync<CrashReportUploadBodyV13>(_jsonSerializerOptions, ct) is not { CrashReport: { } crashReport, LogSources: { } logSources })
        {
            _logger.LogWarning("Failed to read JSON body");
            return controller.StatusCode(StatusCodes.Status500InternalServerError);
        }

        if (await _dbContext.IdEntities.FirstOrDefaultAsync(x => x.CrashReportId == crashReport.Id, ct) is { } idEntity)
            return controller.Ok($"{_options.BaseUri}/{idEntity.FileId}");

        var json = JsonSerializer.Serialize(crashReport, _jsonSerializerOptionsWeb);
        var html = CrashReportHtml.Build(crashReport, logSources);

        controller.Request.Body.Seek(0, SeekOrigin.Begin);
        await using var compressedHtmlStream = await _gZipCompressor.CompressAsync(html.AsStream(), ct);

        await _dbContext.ReportEntities.AddAsync(new ReportEntity
        {
            CrashReportId = crashReport.Id,
            Tenant = tenant,
            Version = crashReport.Version,
            Created = DateTime.UtcNow,
        }, ct);
        await _dbContext.IdEntities.AddAsync(idEntity = new IdEntity { CrashReportId = crashReport.Id, FileId = _fileIdGenerator.Generate(ct), }, ct);
        await _dbContext.HtmlEntities.AddAsync(new HtmlEntity { CrashReportId = crashReport.Id, DataCompressed = compressedHtmlStream.ToArray(), }, ct);
        await _dbContext.JsonEntities.AddAsync(new JsonEntity { CrashReportId = crashReport.Id, Json = json, }, ct);
        await _dbContext.SaveChangesAsync(ct);

        _reportTenant.Add(1, new[] { new KeyValuePair<string, object?>("Tenant", tenant) });
        _reportVersion.Add(1, new[] { new KeyValuePair<string, object?>("Version", crashReport.Version) });

        return controller.Ok(tenant == 1 ? $"{_options.BaseUri}/{idEntity.FileId}" : $"{_options.BaseUri}/{tenant}/{idEntity.FileId}");
    }
}