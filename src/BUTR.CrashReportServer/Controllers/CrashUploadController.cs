using BUTR.CrashReportServer.Options;
using BUTR.CrashReportServer.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReportServer.Controllers
{
    [ApiController]
    [Route("/")]
    public class CrashUploadController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly CrashUploadOptions _options;

        public CrashUploadController(ILogger<CrashUploadController> logger, IOptionsSnapshot<CrashUploadOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        }

        [AllowAnonymous]
        [HttpPost("services/crash-upload.py")]
        [Consumes("text/html")]
        [ProducesResponseType(typeof(void), StatusCodes.Status200OK, "text/plain")]
        [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
        public async Task<IActionResult> CrashUploadAsync([FromServices] IFilePathProvider fileNameProvider, CancellationToken ct)
        {
            if (Request.ContentLength is not { } contentLength || contentLength < _options.MinContentLength || contentLength > _options.MaxContentLength)
                return StatusCode((int) HttpStatusCode.InternalServerError);

            var filePath = await fileNameProvider.GenerateUniqueFilePath(ct);
            if (string.IsNullOrEmpty(filePath))
                return StatusCode((int) HttpStatusCode.InternalServerError);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await Request.Body.CopyToAsync(fs, ct);
            return Created($"{_options.BaseUri}/{Path.GetFileName(filePath)}", $"{_options.BaseUri}/{Path.GetFileName(filePath)}");
        }
    }
}