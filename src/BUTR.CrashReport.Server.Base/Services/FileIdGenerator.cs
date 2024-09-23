using BUTR.CrashReport.Server.Contexts;

using Microsoft.Extensions.Logging;

using System;
using System.Linq;
using System.Threading;

namespace BUTR.CrashReport.Server.Services;

public class FileIdGenerator
{
    private readonly ILogger _logger;
    private readonly AppDbContext _dbContext;
    private readonly HexGenerator _hexGenerator;

    public FileIdGenerator(ILogger<FileIdGenerator> logger, AppDbContext dbContext, HexGenerator hexGenerator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _hexGenerator = hexGenerator ?? throw new ArgumentNullException(nameof(hexGenerator));
    }

    public string Generate(CancellationToken ct)
    {
        const int count = 300;
        var fileId = string.Empty;
        while (!ct.IsCancellationRequested)
        {
            var fileIds = _hexGenerator.GetHex(count, 3);
            var existing = _dbContext.IdEntities.Select(x => x.FileId).Where(x => fileIds.Contains(x)).ToHashSet();
            if (existing.Count == fileIds.Count) continue;
            if (existing.Count == 0) return fileIds.First();
            return fileIds.First(x => !existing.Contains(x));
        }
        return fileId;
    }
}