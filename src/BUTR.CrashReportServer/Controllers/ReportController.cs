using BUTR.CrashReportServer.Models;
using BUTR.CrashReportServer.Options;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Authentication;

namespace BUTR.CrashReportServer.Controllers
{
    [ApiController]
    [Route("/report")]
    [HttpsProtocol(Protocol = SslProtocols.Tls12)]
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
        [RequireHttps()]
        [HttpDelete("Delete/{filename}")]
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
        [HttpGet("GetAllFilenames")]
        [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
        public IActionResult GetAllFilenames() => Ok(Directory.EnumerateFiles(_options.Path ?? string.Empty, "*.html", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(ValidateFileName));
            
        [Authorize]
        [HttpPost("GetFilenameDates")]
        [ProducesResponseType(typeof(FilenameDate[]), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
        public IActionResult GetFilenameDates(ICollection<string> filenames) => Ok(Directory.EnumerateFiles(_options.Path ?? string.Empty, "*.html", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Where(filenames.Contains)
            .Select(x => new FilenameDate(x, new FileInfo(Path.GetFullPath(Path.Combine(_options.Path ?? string.Empty, $"{x}.html"))).CreationTimeUtc.ToString("O"))));
    }
}