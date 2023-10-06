using BUTR.CrashReportServer.Contexts;
using BUTR.CrashReportServer.Models;
using BUTR.CrashReportServer.Models.API;
using BUTR.CrashReportServer.Models.Database;
using BUTR.CrashReportServer.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReportServer.Controllers;

[ApiController]
[Route("/report")]
public class ReportController : ControllerBase
{
    public sealed record GetNewCrashReportsBody
    {
        public required DateTime DateTime { get; init; }
    }

    private readonly ILogger _logger;
    private readonly AppDbContext _dbContext;
    private readonly GZipCompressor _gZipCompressor;

    public ReportController(ILogger<ReportController> logger, AppDbContext dbContext, GZipCompressor gZipCompressor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _gZipCompressor = gZipCompressor ?? throw new ArgumentNullException(nameof(gZipCompressor));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHex(char c) => IsInRange(c, 'A', 'F') || IsInRange(c, '0', '9');

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInRange(char c, char min, char max) => (uint) (c - min) <= (uint) (max - min);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ValidateFileName(string? fileName) => fileName?.Length is 6 or 8 or 10 && fileName.All(IsHex);

    private IActionResult? ValidateRequest(ref string filename)
    {
        if (string.IsNullOrEmpty(filename))
            return StatusCode((int) HttpStatusCode.InternalServerError);

        filename = Path.GetFileNameWithoutExtension(filename);

        if (!ValidateFileName(filename))
            return StatusCode((int) HttpStatusCode.InternalServerError);

        return null;
    }

    private async Task<IActionResult> GetHtml(string filename, CancellationToken ct)
    {
        if (ValidateRequest(ref filename) is { } errorResponse)
            return errorResponse;

        if (await _dbContext.Set<FileEntity>().FirstOrDefaultAsync(x => x.Id.FileId == filename, ct) is not { } file)
            return StatusCode(StatusCodes.Status404NotFound);

        if (Request.GetTypedHeaders().AcceptEncoding.Any(x => x.Value.Equals("gzip", StringComparison.InvariantCultureIgnoreCase)))
        {
            Response.Headers.ContentEncoding = "gzip";
            return File(file.DataCompressed, "text/html; charset=utf-8", true);
        }
        return File(await _gZipCompressor.DecompressAsync(file.DataCompressed, ct), "text/html; charset=utf-8", true);
    }

    private async Task<IActionResult> GetJson(string filename, CancellationToken ct)
    {
        if (ValidateRequest(ref filename) is { } errorResponse)
            return errorResponse;

        if (await _dbContext.Set<JsonEntity>().FirstOrDefaultAsync(x => x.Id.FileId == filename, ct) is not { } file)
            return StatusCode(StatusCodes.Status404NotFound);

        if (Request.GetTypedHeaders().AcceptEncoding.Any(x => x.Value.Equals("gzip", StringComparison.InvariantCultureIgnoreCase)))
        {
            Response.Headers.ContentEncoding = "gzip";
            return File(file.CrashReportCompressed, "application/json; charset=utf-8", true);
        }
        return File(await _gZipCompressor.DecompressAsync(file.CrashReportCompressed, ct), "application/json; charset=utf-8", true);
    }

    [AllowAnonymous]
    [HttpGet("{filename}.html")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK, "text/html")]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public Task<IActionResult> ReportHtml(string filename, CancellationToken ct) => GetHtml(filename, ct);

    [AllowAnonymous]
    [HttpGet("{filename}.json")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public Task<IActionResult> ReportJson(string filename, CancellationToken ct) => GetJson(filename, ct);

    [AllowAnonymous]
    [HttpGet("{filename}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public Task<IActionResult> ReportBasedOnAccept(string filename, CancellationToken ct) => Request.Headers.Accept.FirstOrDefault(x => x is "text/html" or "application/json") switch
    {
        "text/html" => GetHtml(filename, ct),
        "application/json" => GetJson(filename, ct),
        _ => GetHtml(filename, ct),
    };

    [Authorize]
    [HttpDelete("Delete/{filename}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType(typeof(TLSError), StatusCodes.Status400BadRequest, "application/json")]
    [HttpsProtocol(Protocol = SslProtocols.Tls13)]
    public async Task<IActionResult> Delete(string filename)
    {
        if (await _dbContext.Set<FileEntity>().Where(x => x.Id.FileId == filename).ExecuteDeleteAsync(CancellationToken.None) == 0)
            return StatusCode(StatusCodes.Status404NotFound);

        await _dbContext.Set<JsonEntity>().Where(x => x.Id.FileId == filename).ExecuteDeleteAsync(CancellationToken.None);

        return Ok();
    }

    [Authorize]
    [HttpGet("GetAllFilenames")]
    [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType(typeof(TLSError), StatusCodes.Status400BadRequest, "application/json")]
    [HttpsProtocol(Protocol = SslProtocols.Tls13)]
    public ActionResult<IAsyncEnumerable<string>> GetAllFilenames() => Ok(_dbContext.Set<IdEntity>().Select(x => x.FileId));

    [Authorize]
    [HttpPost("GetMetadata")]
    [ProducesResponseType(typeof(FileMetadata[]), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType(typeof(TLSError), StatusCodes.Status400BadRequest, "application/json")]
    [HttpsProtocol(Protocol = SslProtocols.Tls13)]
    public ActionResult<IEnumerable<FileMetadata>> GetFilenameDates(ICollection<string> filenames, CancellationToken ct)
    {
        var filenamesWithExtension = filenames.Select(Path.GetFileNameWithoutExtension).ToImmutableArray();

        return Ok(_dbContext.Set<IdEntity>()
            .Where(x => filenamesWithExtension.Contains(x.FileId))
            .Select(x => new { x.FileId, x.CrashReportId, x.Version, x.Created })
            .AsAsyncEnumerable()
            .Select(x => new FileMetadata(x.FileId, x.CrashReportId, x.Version, x.Created.ToUniversalTime())));
    }

    [Authorize]
    [HttpPost("GetNewCrashReports")]
    [ProducesResponseType(typeof(FileMetadata[]), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType(typeof(TLSError), StatusCodes.Status400BadRequest, "application/json")]
    [HttpsProtocol(Protocol = SslProtocols.Tls13)]
    public ActionResult<IEnumerable<FileMetadata>> GetNewCrashReportsDates([FromBody] GetNewCrashReportsBody body, CancellationToken ct)
    {
        var diff = DateTime.UtcNow - body.DateTime;
        if (diff.Ticks < 0 || DateTime.UtcNow - body.DateTime > TimeSpan.FromDays(7))
            return BadRequest();

        return Ok(_dbContext.Set<IdEntity>()
            .Where(x => x.Created >= body.DateTime)
            .Select(x => new { x.FileId, x.CrashReportId, x.Version, x.Created })
            .AsAsyncEnumerable()
            .Select(x => new FileMetadata(x.FileId, x.CrashReportId, x.Version, x.Created.ToUniversalTime())));
    }
}