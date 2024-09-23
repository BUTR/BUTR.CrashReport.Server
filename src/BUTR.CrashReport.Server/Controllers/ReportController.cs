using BUTR.CrashReport.Server.Contexts;
using BUTR.CrashReport.Server.Models;
using BUTR.CrashReport.Server.Models.API;
using BUTR.CrashReport.Server.Models.Sitemaps;
using BUTR.CrashReport.Server.Options;
using BUTR.CrashReport.Server.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

namespace BUTR.CrashReport.Server.Controllers;

[ApiController]
[Route("/report")]
public class ReportController : ControllerBase
{
    public sealed record GetNewCrashReportsBody(DateTime DateTime);

    private readonly ILogger _logger;
    private readonly ReportOptions _options;
    private readonly AppDbContext _dbContext;
    private readonly GZipCompressor _gZipCompressor;

    public ReportController(ILogger<ReportController> logger, IOptionsSnapshot<ReportOptions> options, AppDbContext dbContext, GZipCompressor gZipCompressor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _gZipCompressor = gZipCompressor ?? throw new ArgumentNullException(nameof(gZipCompressor));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHex(char c) => IsInRange(c, 'A', 'F') || IsInRange(c, '0', '9');

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInRange(char c, char min, char max) => (uint) (c - min) <= (uint) (max - min);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ValidateFileName(string? fileName) => fileName?.Length is 6 or 8 or 10 && fileName.All(IsHex);

    private StatusCodeResult? ValidateRequest(string filename)
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
        if (ValidateRequest(filename) is { } errorResponse)
            return errorResponse;

        if (await _dbContext.FileEntities.FirstOrDefaultAsync(x => x.Id!.FileId == filename, ct) is not { } file)
            return StatusCode(StatusCodes.Status404NotFound);

        if (Request.GetTypedHeaders().AcceptEncoding.Any(x => x.Value.Equals("gzip", StringComparison.InvariantCultureIgnoreCase)))
        {
            Response.Headers.ContentEncoding = "gzip";
            return File(file.DataCompressed, "text/html; charset=utf-8", false);
        }
        return File(await _gZipCompressor.DecompressAsync(file.DataCompressed, ct), "text/html; charset=utf-8", false);
    }

    private async Task<IActionResult> GetJson(string filename, CancellationToken ct)
    {
        if (ValidateRequest(filename) is { } errorResponse)
            return errorResponse;

        if (await _dbContext.JsonEntities.FirstOrDefaultAsync(x => x.Id!.FileId == filename, ct) is not { } file)
            return StatusCode(StatusCodes.Status404NotFound);

        return Content(file.CrashReport, "application/json; charset=utf-8");
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
        if (await _dbContext.FileEntities.Where(x => x.Id!.FileId == filename).ExecuteDeleteAsync(CancellationToken.None) == 0)
            return StatusCode(StatusCodes.Status404NotFound);

        await _dbContext.JsonEntities.Where(x => x.Id!.FileId == filename).ExecuteDeleteAsync(CancellationToken.None);

        return Ok();
    }

    [Authorize]
    [HttpGet("GetAllFilenames")]
    [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType(typeof(TLSError), StatusCodes.Status400BadRequest, "application/json")]
    [HttpsProtocol(Protocol = SslProtocols.Tls13)]
    public ActionResult<IAsyncEnumerable<string>> GetAllFilenames() => Ok(_dbContext.IdEntities.Select(x => x.FileId));

    [Authorize]
    [HttpPost("GetMetadata")]
    [ProducesResponseType(typeof(FileMetadata[]), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType(typeof(TLSError), StatusCodes.Status400BadRequest, "application/json")]
    [HttpsProtocol(Protocol = SslProtocols.Tls13)]
    public ActionResult<IEnumerable<FileMetadata>> GetFilenameDates(ICollection<string> filenames, CancellationToken ct)
    {
        var filenamesWithExtension = filenames.Select(Path.GetFileNameWithoutExtension).ToImmutableArray();

        return Ok(_dbContext.IdEntities
            .Where(x => filenamesWithExtension.Contains(x.FileId))
            .AsEnumerable()
            .Select(x => new FileMetadata
            {
                File = x.FileId,
                Id = x.CrashReportId,
                Version = x.Version,
                Date = x.Created,
            }));
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
        if (diff.Ticks < 0 || diff > TimeSpan.FromDays(30))
            return BadRequest();

        return Ok(_dbContext.IdEntities
            .Where(x => x.Created > body.DateTime)
            .Select(x => new FileMetadata
            {
                File = x.FileId,
                Id = x.CrashReportId,
                Version = x.Version,
                Date = x.Created,
            }));
    }

    [AllowAnonymous]
    [HttpGet("sitemap_index.xml")]
    [Produces("application/xml")]
    [ProducesResponseType(typeof(Urlset), StatusCodes.Status200OK, "application/xml")]
    [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+xml")]
    [ResponseCache(Duration = 60 * 60 * 4)]
    public IActionResult SitemapIndex()
    {
        var count = _dbContext.IdEntities.Count();
        var sitemaps = (count / 50000) + 1;

        var sitemap = new SitemapIndex
        {
            Sitemap = Enumerable.Range(0, sitemaps).Select(x => new Sitemap
            {
                Location = $"{_options.BaseUri}/sitemap_{x}.xml",
            }).ToList(),
        };
        return Ok(sitemap);
    }

    [AllowAnonymous]
    [HttpGet("sitemap_{idx:int}.xml")]
    [Produces("application/xml")]
    [ProducesResponseType(typeof(Urlset), StatusCodes.Status200OK, "application/xml")]
    [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+xml")]
    [ResponseCache(Duration = 60 * 60 * 4)]
    public IActionResult Sitemap(int idx)
    {
        var sitemap = new Urlset
        {
            Url = _dbContext.IdEntities.Skip(idx * 50000).Take(50000).Select(x => new { x.FileId, x.Created }).Select(x => new Url
            {
                Location = $"{_options.BaseUri}/{x.FileId}",
                TimeStamp = x.Created,
                Priority = 0.5,
                ChangeFrequency = ChangeFrequency.Never,
            }).ToList(),
        };
        return Ok(sitemap);
    }
}