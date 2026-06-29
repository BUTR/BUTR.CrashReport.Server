using BUTR.CrashReport.Renderer.Html;
using BUTR.CrashReport.Server.Extensions;
using BUTR.CrashReport.Server.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.IO.Compression;
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
    private readonly CrashReportStore _store;
    private JsonSerializerOptions _jsonSerializerOptions;

    public JsonHandlerV13(
        ILogger<JsonHandlerV13> logger,
        IOptionsMonitor<JsonSerializerOptions> jsonSerializerOptions,
        CrashReportStore store)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _store = store ?? throw new ArgumentNullException(nameof(store));

        _jsonSerializerOptions = jsonSerializerOptions.CurrentValue ?? throw new ArgumentNullException(nameof(jsonSerializerOptions));
        jsonSerializerOptions.OnChange(x => _jsonSerializerOptions = x);
    }

    public async Task<IActionResult> UploadJsonAsync(ControllerBase controller, CancellationToken ct)
    {
        var tenant = byte.TryParse(controller.Request.Headers["Tenant"].ToString(), out var tenantId) ? tenantId : (byte) 1;

        // Stream the gzip body through the decoder instead of buffering the whole decompressed payload in memory.
        // ReadFromJsonAsync pulls bytes incrementally, so the full body is never materialized.
        if (controller.Request.Headers.ContentEncoding is ["gzip", "deflate"] or ["gzip,deflate"] or ["gzip, deflate"])
            controller.Request.Body = new GZipStream(controller.Request.Body, CompressionMode.Decompress);

        if (await controller.HttpContext.Request.ReadFromJsonAsync<CrashReportUploadBodyV13>(_jsonSerializerOptions, ct) is not { CrashReport: { } crashReport, LogSources: { } logSources })
        {
            _logger.LogWarning("Failed to read JSON body");
            return controller.StatusCode(StatusCodes.Status500InternalServerError);
        }

        var json = JsonSerializer.Serialize(crashReport, _jsonSerializerOptionsWeb);
        var html = CrashReportHtml.Build(crashReport, logSources);

        var result = await _store.StoreAsync(crashReport.Id, tenant, crashReport.Version, html.AsStream(), json, ct);
        return result.Respond(controller);
    }
}