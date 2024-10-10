using BUTR.CrashReport.Bannerlord.Parser;
using BUTR.CrashReport.Models;
using BUTR.CrashReport.Server.Contexts;
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
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReport.Server.v14;

public class HtmlHandlerV14
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

    private readonly Counter<int> _reportTenant;
    private readonly Counter<int> _reportVersion;

    public HtmlHandlerV14(
        ILogger<HtmlHandlerV14> logger,
        IOptionsMonitor<CrashUploadOptions> options,
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

        _options = options.CurrentValue ?? throw new ArgumentNullException(nameof(options));
        options.OnChange(x => _options = x);
    }

    public async Task<IActionResult> UploadHtmlAsync(ControllerBase controller, CancellationToken ct)
    {
        controller.Request.EnableBuffering();

        var tenant = byte.TryParse(controller.Request.Headers["Tenant"].ToString(), out var tenantId) ? tenantId : (byte) 1;

        using var streamReader = new StreamReader(controller.Request.Body);
        var html = await streamReader.ReadToEndAsync(ct);
        var (valid, version, crashReportModel) = ParseHtml(html);
        if (!valid || crashReportModel is null)
        {
            _logger.LogWarning("Invalid HTML");
            return controller.StatusCode(StatusCodes.Status500InternalServerError);
        }

        if (await _dbContext.IdEntities.FirstOrDefaultAsync(x => x.CrashReportId == crashReportModel.Id, ct) is { } idEntity)
            return controller.Ok($"{_options.BaseUri}/{idEntity.FileId}");

        var json = JsonSerializer.Serialize(crashReportModel, _jsonSerializerOptionsWeb);

        controller.Request.Body.Seek(0, SeekOrigin.Begin);
        await using var compressedHtmlStream = await _gZipCompressor.CompressAsync(controller.Request.Body, ct);

        await _dbContext.ReportEntities.AddAsync(new ReportEntity
        {
            CrashReportId = crashReportModel.Id,
            Tenant = tenant,
            Version = version,
            Created = DateTime.UtcNow,
        }, ct);
        await _dbContext.IdEntities.AddAsync(idEntity = new IdEntity { CrashReportId = crashReportModel.Id, FileId = _fileIdGenerator.Generate(ct), }, ct);
        await _dbContext.HtmlEntities.AddAsync(new HtmlEntity { CrashReportId = crashReportModel.Id, DataCompressed = compressedHtmlStream.ToArray(), }, ct);
        if (version >= 13) await _dbContext.JsonEntities.AddAsync(new JsonEntity { CrashReportId = crashReportModel.Id, Json = json, }, ct);
        await _dbContext.SaveChangesAsync(ct);

        _reportTenant.Add(1, new[] { new KeyValuePair<string, object?>("Tenant", tenant) });
        _reportVersion.Add(1, new[] { new KeyValuePair<string, object?>("Version", version) });

        return controller.Ok(tenant == 1 ? $"{_options.BaseUri}/{idEntity.FileId}" : $"{_options.BaseUri}/{tenant}/{idEntity.FileId}");
    }

    private static (bool isValid, byte version, CrashReportModel? crashReportModel) ParseHtml(string html)
    {
        var valid = CrashReportParser.TryParse(html, out var version, out var crashReportModel, out _);
        return (valid, version, crashReportModel);
    }
}