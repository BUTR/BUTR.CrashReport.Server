using BUTR.CrashReport.Bannerlord.Parser;
using BUTR.CrashReport.Models;
using BUTR.CrashReport.Renderer.Html;
using BUTR.CrashReport.Server.Extensions;
using BUTR.CrashReport.Server.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReport.Server.v13;

public class HtmlHandlerV13
{
    /// <summary>The renderer's HTML and the serialized model for a legacy report, ready to persist.</summary>
    public sealed record RebuiltLegacyReport(Guid CrashReportId, byte Version, string Html, string Json);

    private static readonly JsonSerializerOptions _jsonSerializerOptionsWeb = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ILogger _logger;
    private readonly CrashReportStore _store;

    public HtmlHandlerV13(ILogger<HtmlHandlerV13> logger, CrashReportStore store)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<IActionResult> UploadHtmlAsync(ControllerBase controller, string html, CancellationToken ct)
    {
        var tenant = byte.TryParse(controller.Request.Headers["Tenant"].ToString(), out var tenantId) ? tenantId : (byte) 1;

        // Never persist the uploaded HTML; rebuild it from the parsed model with the trusted renderer and store that
        // (plus the model as JSON). The attacker-controlled upload bytes are dropped.
        if (Rebuild(html) is not { } rebuilt)
        {
            _logger.LogWarning("Invalid HTML");
            return controller.StatusCode(StatusCodes.Status500InternalServerError);
        }

        var result = await _store.StoreAsync(rebuilt.CrashReportId, tenant, rebuilt.Version, rebuilt.Html.AsStream(), rebuilt.Json, ct);
        return result.Respond(controller);
    }

    /// <summary>
    /// Parses a legacy (&lt;13) report's HTML and returns the trusted renderer's HTML plus the serialized model,
    /// or null when the input isn't a parseable legacy report. Used by the upload path and the one-off re-render
    /// migration that replaces stored originals with sanitized output.
    /// </summary>
    public static RebuiltLegacyReport? Rebuild(string originalHtml)
    {
        var (valid, version, crashReportModel) = ParseHtml(originalHtml);
        if (!valid || crashReportModel is null || version >= 13)
            return null;

        var html = CrashReportHtml.Build(crashReportModel, ParseLogs(originalHtml));
        var json = JsonSerializer.Serialize(crashReportModel, _jsonSerializerOptionsWeb);
        return new RebuiltLegacyReport(crashReportModel.Id, version, html, json);
    }

    // Legacy (<13) reports embed their log sources in the HTML; recover them so the rebuilt report keeps them.
    private static IEnumerable<LogSource> ParseLogs(string html)
    {
        try { return CrashReportParser.ParseLegacyHtmlLogs(html); }
        catch { return []; }
    }

    private static (bool isValid, byte version, CrashReportModel? crashReportModel) ParseHtml(string html)
    {
        var valid = CrashReportParser.TryParse(html, out var version, out var crashReportModel, out _);
        return (valid, version, crashReportModel);
    }
}