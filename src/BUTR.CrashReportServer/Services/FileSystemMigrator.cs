using BUTR.CrashReportServer.Contexts;
using BUTR.CrashReportServer.Models.Database;
using BUTR.CrashReportServer.Options;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReportServer.Services
{
    public sealed class FileSystemMigrator : BackgroundService
    {
        private readonly AppDbContext _dbContext;
        private readonly StorageOptions _storageOptions;
        private readonly GZipCompressor _gZipCompressor;

        public FileSystemMigrator(AppDbContext dbContext, IOptions<StorageOptions> storageOptions, GZipCompressor gZipCompressor)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _storageOptions = storageOptions.Value ?? throw new ArgumentNullException(nameof(storageOptions));
            _gZipCompressor = gZipCompressor ?? throw new ArgumentNullException(nameof(gZipCompressor));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var path = _storageOptions.Path ?? string.Empty;
            Directory.CreateDirectory(Path.Combine(path, "copied"));
            
            var fileChunks = new DirectoryInfo(path).EnumerateFiles("*.html", SearchOption.TopDirectoryOnly)
                .Chunk(10)
                .ToAsyncEnumerable()
                .WithCancellation(stoppingToken);
            
            await foreach (var files in fileChunks)
            {
                foreach (var file in files)
                {
                    using var compressed = await _gZipCompressor.CompressAsync(file.OpenRead(), stoppingToken);
                    await _dbContext.Set<FileEntity>().AddAsync(new FileEntity
                    {
                        Name = file.Name,
                        Created = file.CreationTimeUtc,
                        Modified = file.LastWriteTimeUtc,
                        SizeOriginal = file.Length,
                        DataCompressed = compressed.ToArray()
                    }, stoppingToken);
                }
                await _dbContext.SaveChangesAsync(stoppingToken);
                
                foreach (var file in files)
                    file.MoveTo(Path.Combine(path, "copied", file.Name));
            }
        }
    }
}