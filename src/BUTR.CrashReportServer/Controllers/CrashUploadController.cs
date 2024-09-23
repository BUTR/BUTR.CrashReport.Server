using BUTR.CrashReportServer.Options;
using BUTR.CrashReportServer.v13;
using BUTR.CrashReportServer.v14;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReportServer.Controllers;

[ApiController]
[Route("/services")]
public class CrashUploadController : ControllerBase
{
    private readonly ILogger _logger;
    private readonly CrashUploadOptions _options;
    private readonly JsonHandlerV13 _jsonHandlerV13;
    private readonly HtmlHandlerV14 _htmlHandlerV14;
    private readonly JsonHandlerV14 _jsonHandlerV14;

    public CrashUploadController(
        ILogger<CrashUploadController> logger,
        IOptionsSnapshot<CrashUploadOptions> options,
        JsonHandlerV13 jsonHandlerV13,
        HtmlHandlerV14 htmlHandlerV14,
        JsonHandlerV14 jsonHandlerV14)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _jsonHandlerV13 = jsonHandlerV13;
        _htmlHandlerV14 = htmlHandlerV14;
        _jsonHandlerV14 = jsonHandlerV14;
    }

    [AllowAnonymous]
    [HttpPost("crash-upload.py")]
    //[Consumes(typeof(CrashReportUploadBodyV13), "application/json", "text/html")]
    //[Consumes(typeof(CrashReportUploadBodyV14), "application/json", "text/html")]
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
                        return _jsonHandlerV13.UploadJsonAsync(this, ct);
                    case "14":
                        return _jsonHandlerV14.UploadJsonAsync(this, ct);
                    default:
                        return _jsonHandlerV14.UploadJsonAsync(this, ct);
                }
            }
            case "text/html":
            default:
            {
                return _htmlHandlerV14.UploadHtmlAsync(this, ct);
            }
        }
    }
}