using BUTR.CrashReportServer.Contexts;
using BUTR.CrashReportServer.Models.Database;
using BUTR.CrashReportServer.Options;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using DirectoryInfo = System.IO.DirectoryInfo;

namespace BUTR.CrashReportServer.Services
{
    public sealed class FileSystemMigrator : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly StorageOptions _storageOptions;
        private readonly GZipCompressor _gZipCompressor;

        public FileSystemMigrator(IServiceScopeFactory scopeFactory, IOptions<StorageOptions> storageOptions, GZipCompressor gZipCompressor)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _storageOptions = storageOptions.Value ?? throw new ArgumentNullException(nameof(storageOptions));
            _gZipCompressor = gZipCompressor ?? throw new ArgumentNullException(nameof(gZipCompressor));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var path = _storageOptions.Path ?? string.Empty;
            Directory.CreateDirectory(Path.Combine(path, "copied"));
            Directory.CreateDirectory(Path.Combine(path, "copied2"));
            
            var fileChunks = new DirectoryInfo(path).EnumerateFiles("*.html", SearchOption.TopDirectoryOnly)
                .Concat(new DirectoryInfo(Path.Combine(path, "copied")).EnumerateFiles("*.html", SearchOption.TopDirectoryOnly))
                .Chunk(10)
                .ToAsyncEnumerable()
                .WithCancellation(stoppingToken);
            
            await foreach (var files in fileChunks)
            {
                foreach (var file in files)
                {
                    using var compressed = await _gZipCompressor.CompressAsync(file.OpenRead(), stoppingToken);
                    await dbContext.Set<FileEntity>().AddAsync(new FileEntity
                    {
                        Name = file.Name,
                        Created = file.CreationTimeUtc,
                        Modified = file.LastWriteTimeUtc,
                        SizeOriginal = file.Length,
                        DataCompressed = compressed.ToArray()
                    }, stoppingToken);
                }
                await dbContext.SaveChangesAsync(stoppingToken);
                
                foreach (var file in files)
                    file.MoveTo(Path.Combine(path, "copied2", file.Name));
            }
        }
    }
}