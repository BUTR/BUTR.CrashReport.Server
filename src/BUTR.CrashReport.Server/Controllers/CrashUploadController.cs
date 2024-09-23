using BUTR.CrashReport.Server.Options;
using BUTR.CrashReport.Server.v13;
using BUTR.CrashReport.Server.v14;

using HtmlAgilityPack;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReport.Server.Controllers;

[ApiController]
[Route("/services")]
public class CrashUploadController : ControllerBase
{
    private readonly ILogger _logger;
    private readonly CrashUploadOptions _options;
    private readonly HtmlHandlerV13 _htmlHandlerV13;
    private readonly JsonHandlerV13 _jsonHandlerV13;
    private readonly HtmlHandlerV14 _htmlHandlerV14;
    private readonly JsonHandlerV14 _jsonHandlerV14;

    public CrashUploadController(
        ILogger<CrashUploadController> logger,
        IOptionsSnapshot<CrashUploadOptions> options,
        HtmlHandlerV13 htmlHandlerV13,
        JsonHandlerV13 jsonHandlerV13,
        HtmlHandlerV14 htmlHandlerV14,
        JsonHandlerV14 jsonHandlerV14)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _htmlHandlerV13 = htmlHandlerV13;
        _jsonHandlerV13 = jsonHandlerV13;
        _htmlHandlerV14 = htmlHandlerV14;
        _jsonHandlerV14 = jsonHandlerV14;
    }

    [AllowAnonymous]
    [HttpPost("crash-upload.py")]
    [Consumes("application/json", "text/html")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK, "text/plain")]
    [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public Task<IActionResult> CrashUploadAsync(CancellationToken ct)
    {
        if (Request.ContentLength is not { } contentLength || contentLength < _options.MinContentLength || contentLength > _options.MaxContentLength)
        {
            _logger.LogWarning("Content length is invalid: {ContentLength}. Min: {MinContentLength}; Max: {MaxContentLength}", Request.ContentLength, _options.MinContentLength, _options.MaxContentLength);
            return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status500InternalServerError));
        }

        switch (Request.ContentType)
        {
            case "application/json":
                return UploadJsonAsync(ct);
            case "text/html":
            default:
                return UploadHtmlAsync(ct);
        }
    }

    private async Task<IActionResult> UploadJsonAsync(CancellationToken ct)
    {
        var version = Request.Headers["CrashReportVersion"].ToString();
        switch (version)
        {
            case "13":
                return await _jsonHandlerV13.UploadJsonAsync(this, ct);
            case "14":
                return await _jsonHandlerV14.UploadJsonAsync(this, ct);
            default:
            {
                _logger.LogWarning("Crash report version is invalid: {CrashReportVersion}", version);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
    }

    private async Task<IActionResult> UploadHtmlAsync(CancellationToken ct)
    {
        static HtmlDocument Create(ref string content)
        {
            content = content.Replace("<filename unknown>", "NULL");
            var document = new HtmlDocument();
            document.LoadHtml(content);
            return document;
        }
        static bool TryParseVersion(string content, out byte version)
        {
            try
            {
                var document = Create(ref content);

                var versionStr = document.DocumentNode.SelectSingleNode("descendant::report")?.Attributes?["version"]?.Value;
                version = byte.TryParse(versionStr, out var v) ? v : (byte) 1;
                return true;
            }
            catch (Exception)
            {
                version = 0;
                return false;
            }
        }

        Request.EnableBuffering();

        using var streamReader = new StreamReader(Request.Body);
        var html = await streamReader.ReadToEndAsync(ct);
        if (!TryParseVersion(html, out var version))
        {
            _logger.LogWarning("Failed to parse html crash report version");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        if (version <= 13)
            return await _htmlHandlerV13.UploadHtmlAsync(this, ct);
        if (version == 14)
            return await _htmlHandlerV14.UploadHtmlAsync(this, ct);

        _logger.LogWarning("Crash report version is invalid: {CrashReportVersion}", version);
        return StatusCode(StatusCodes.Status500InternalServerError);
    }
}