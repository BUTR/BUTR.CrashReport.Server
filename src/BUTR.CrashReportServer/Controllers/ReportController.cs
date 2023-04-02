using BUTR.CrashReportServer.Contexts;
using BUTR.CrashReportServer.Models;
using BUTR.CrashReportServer.Models.API;
using BUTR.CrashReportServer.Models.Database;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace BUTR.CrashReportServer.Controllers
{
    [ApiController]
    [Route("/report")]
    public class ReportController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly AppDbContext _dbContext;

        public ReportController(ILogger<ReportController> logger, AppDbContext dbContext)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHex(char c) => IsInRange(c, 'A', 'F') || IsInRange(c, '0', '9');

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInRange(char c, char min, char max) => (uint) (c - min) <= (uint) (max - min);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ValidateFileName(string? fileName) => fileName?.Length is 6 or 8 or 10 && fileName.All(IsHex);

        [AllowAnonymous]
        [HttpGet("{filename}")]
        [ProducesResponseType(typeof(void), StatusCodes.Status200OK, "text/html")]
        [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
        public async Task<IActionResult> Report(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return StatusCode((int) HttpStatusCode.InternalServerError);

            if (!string.Equals(Path.GetExtension(filename), ".html", StringComparison.Ordinal))
                filename += ".html";

            if (!ValidateFileName(Path.GetFileNameWithoutExtension(filename)))
                return StatusCode((int) HttpStatusCode.InternalServerError);

            if (await _dbContext.Set<FileEntity>().FirstOrDefaultAsync(x => x.Name == filename) is not { } file)
                return StatusCode((int) HttpStatusCode.NotFound);

            if (Request.GetTypedHeaders().AcceptEncoding.Any(x => x.Value.Equals("gzip", StringComparison.InvariantCultureIgnoreCase)))
            {
                Response.Headers.ContentEncoding = "gzip";
                return File(file.DataCompressed, "text/html; charset=utf-8", true);
            }
            return File(new GZipStream(new MemoryStream(file.DataCompressed), CompressionMode.Decompress), "text/html; charset=utf-8", true);
        }

        [Authorize]
        [HttpDelete("Delete/{filename}")]
        [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
        [ProducesResponseType(typeof(TLSError), StatusCodes.Status400BadRequest, "application/json")]
        [HttpsProtocol(Protocol = SslProtocols.Tls12)]
        public async Task<IActionResult> Delete(string filename)
        {
            if (await _dbContext.Set<FileEntity>().Where(x => x.Name == filename).ExecuteDeleteAsync() == 0)
                return StatusCode((int) HttpStatusCode.NotFound);

            return Ok();
        }

        [Authorize]
        [HttpGet("GetAllFilenames")]
        [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
        [ProducesResponseType(typeof(TLSError), StatusCodes.Status400BadRequest, "application/json")]
        [HttpsProtocol(Protocol = SslProtocols.Tls12)]
        public IActionResult GetAllFilenames()
        {
            return Ok(_dbContext.Set<FileEntity>()
                .Select(x => x.Name)
                .Where(x => EF.Functions.Like(x, "%.html"))
                .AsEnumerable()
                .Select(Path.GetFileNameWithoutExtension)
                .Where(ValidateFileName));
        }

        [Authorize]
        [HttpPost("GetFilenameDates")]
        [ProducesResponseType(typeof(FilenameDate[]), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
        [ProducesResponseType(typeof(TLSError), StatusCodes.Status400BadRequest, "application/json")]
        [HttpsProtocol(Protocol = SslProtocols.Tls12)]
        public IActionResult GetFilenameDates(ICollection<string> filenames)
        {
            var filenamesWithExtension = filenames.Select(x =>
            {
                if (!string.Equals(Path.GetExtension(x), ".html", StringComparison.Ordinal))
                    x += ".html";
                return x;
            }).ToArray();
            
            return Ok(_dbContext.Set<FileEntity>()
                .Where(x => filenamesWithExtension.Contains(x.Name))
                .AsEnumerable()
                .Select(x => new FilenameDate(Path.GetFileNameWithoutExtension(x.Name), x.Created.ToString("O"))));
        }
    }
}