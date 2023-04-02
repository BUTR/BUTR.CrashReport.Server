using BUTR.CrashReportServer.Contexts;
using BUTR.CrashReportServer.Models.Database;
using BUTR.CrashReportServer.Options;
using BUTR.CrashReportServer.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Numeral;

using System;
using System.Linq;
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
        private readonly AppDbContext _dbContext;
        private readonly GZipCompressor _gZipCompressor;
        private readonly Random _random;

        public CrashUploadController(ILogger<CrashUploadController> logger, IOptionsSnapshot<CrashUploadOptions> options, AppDbContext dbContext, GZipCompressor gZipCompressor, Random random)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _gZipCompressor = gZipCompressor ?? throw new ArgumentNullException(nameof(gZipCompressor));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        private string GetHex()
        {
            Span<byte> buffer = stackalloc byte[3];
            Span<char> buffer2 = stackalloc char[6];
            _random.NextBytes(buffer);
            HexConverter.GetChars(buffer, buffer2);
            for (var i = 0; i < buffer2.Length; i++)
                buffer2[i] = char.ToUpper(buffer2[i]);
            return buffer2.ToString();
        }
        
        [AllowAnonymous]
        [HttpPost("services/crash-upload.py")]
        [Consumes("text/html")]
        [ProducesResponseType(typeof(void), StatusCodes.Status200OK, "text/plain")]
        [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError, "application/problem+json")]
        public async Task<IActionResult> CrashUploadAsync(CancellationToken ct)
        {
            if (Request.ContentLength is not { } contentLength || contentLength < _options.MinContentLength || contentLength > _options.MaxContentLength)
                return StatusCode((int) HttpStatusCode.InternalServerError);

            string id;
            while (true)
            {
                var unique = Enumerable.Range(0, 10).Select(_ => $"{GetHex()}.html").ToArray();
                var existing = await _dbContext.Set<FileEntity>().Select(x => x.Name).Where(x => unique.Contains(x)).ToArrayAsync(ct);
                if (existing.Length == 10) continue;
                id = unique.Except(existing).First();
                break;
            }

            using var compressed = await _gZipCompressor.CompressAsync(Request.Body, ct);
            var entry = await _dbContext.Set<FileEntity>().AddAsync(new FileEntity
            {
                Name = id,
                Created = DateTimeOffset.UtcNow,
                Modified = DateTimeOffset.UtcNow,
                SizeOriginal = contentLength,
                DataCompressed = compressed.ToArray()
            }, ct);
            await _dbContext.SaveChangesAsync(ct);
            return Ok($"{_options.BaseUri}/{entry.Entity.Name}");
        }
    }
}