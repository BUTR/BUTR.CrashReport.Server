using BUTR.CrashReportServer.Options;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;

namespace BUTR.CrashReportServer.Controllers
{
    [ApiController]
    [Route("/report")]
    public class ReportController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly StorageOptions _options;

        public ReportController(ILogger<ReportController> logger, IOptionsSnapshot<StorageOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHex(char c) => IsInRange(c, 'A', 'F') || IsInRange(c, '0', '9');

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInRange(char c, char min, char max) => (uint) (c - min) <= (uint) (max - min);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ValidateFileName(string fileName) => fileName.Length is 6 or 8 or 10 && fileName.All(IsHex);

        [AllowAnonymous]
        [HttpGet("{filename}")]
        [ProducesResponseType(typeof(void), StatusCodes.Status200OK, "text/html")]
        [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
        public IActionResult Report(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return StatusCode((int) HttpStatusCode.InternalServerError);

            if (!string.Equals(Path.GetExtension(filename), ".html", StringComparison.Ordinal))
                filename += ".html";

            if (!ValidateFileName(Path.GetFileNameWithoutExtension(filename)))
                return StatusCode((int) HttpStatusCode.InternalServerError);

            var filePath = Path.GetFullPath(Path.Combine(_options.Path ?? string.Empty, filename));
            if (!System.IO.File.Exists(filePath))
                return StatusCode((int) HttpStatusCode.NotFound);

            return PhysicalFile(filePath, "text/html; charset=utf-8", true);
            //var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            //return File(fs, "text/html; charset=utf-8", true);
        }

        [Authorize]
        [HttpDelete("{filename}")]
        [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
        public IActionResult Delete(string filename)
        {
            var filePath = Path.GetFullPath(Path.Combine(_options.Path ?? string.Empty, filename));
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            System.IO.File.Delete(filePath);
            return Ok();
        }

        [Authorize]
        [HttpGet("")]
        [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
        public IActionResult List() => Ok(Directory.EnumerateFiles(_options.Path ?? string.Empty, "*.html", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(ValidateFileName));
    }
}