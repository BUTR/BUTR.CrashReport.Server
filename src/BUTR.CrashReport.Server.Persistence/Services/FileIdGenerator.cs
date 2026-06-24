using BUTR.CrashReport.Server.Contexts;

using Microsoft.Extensions.Logging;

using System;
using System.Linq;
using System.Threading;

namespace BUTR.CrashReport.Server.Services;

public class FileIdGenerator
{
    // 6 Crockford Base32 characters = 30 bits of entropy, 64x the space of the previous 6 hex chars (24 bits).
    private const int IdLength = 6;

    private readonly ILogger _logger;
    private readonly AppDbContext _dbContext;
    private readonly Base32Generator _base32Generator;

    public FileIdGenerator(ILogger<FileIdGenerator> logger, AppDbContext dbContext, Base32Generator base32Generator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _base32Generator = base32Generator ?? throw new ArgumentNullException(nameof(base32Generator));
    }

    public string Generate(CancellationToken ct)
    {
        const int count = 300;
        var fileId = string.Empty;
        while (!ct.IsCancellationRequested)
        {
            var fileIds = _base32Generator.GetIds(count, IdLength);
            var existing = _dbContext.IdEntities.Select(x => x.FileId).Where(x => fileIds.Contains(x)).ToHashSet();
            if (existing.Count == fileIds.Count) continue;
            if (existing.Count == 0) return fileIds.First();
            return fileIds.First(x => !existing.Contains(x));
        }
        return fileId;
    }
}