using BUTR.CrashReportServer.Contexts;
using BUTR.CrashReportServer.Models.Database;
using BUTR.CrashReportServer.Utils;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReportServer.Services;

public sealed class DatabaseMigrator : BackgroundService
{
    private class ObjectPool<T>
    {
        private readonly ConcurrentBag<T> _objects = new();
        private readonly Func<CancellationToken, Task<T>> _objectGenerator;

        public ObjectPool(Func<CancellationToken, Task<T>> objectGenerator)
        {
            _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
        }

        public async Task<T> GetAsync(CancellationToken ct) => _objects.TryTake(out var item) ? item : await _objectGenerator(ct);

        public void Return(T item) => _objects.Add(item);
    }


    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GZipCompressor _gZipCompressor;

    public DatabaseMigrator(IServiceScopeFactory scopeFactory, GZipCompressor gZipCompressor)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _gZipCompressor = gZipCompressor ?? throw new ArgumentNullException(nameof(gZipCompressor));
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var dbContextPool = new ObjectPool<AppDbContext>(dbContextFactory.CreateDbContextAsync);

        var options = new ParallelOptions { CancellationToken = ct };
        await Parallel.ForEachAsync(Enumerable.Range(0, 4), options, async (_, ct2) =>
        {
            var dbContext = await dbContextPool.GetAsync(ct2);

            var wrong = dbContext.Set<FileEntity>().AsNoTracking().Where(x => x.Id.Version == 0).Take(1000);
            while (true)
            {
                var entities = wrong.ToArray();
                if (entities.Length == 0) break;

                var sb = new StringBuilder();
                sb.AppendLine("BEGIN TRANSACTION;");
                foreach (var entity in entities)
                {
                    await using var decompressed = await _gZipCompressor.DecompressAsync(entity.DataCompressed, ct);
                    decompressed.Seek(0, SeekOrigin.Begin);
                    decompressed.Seek(0, SeekOrigin.Begin);

                    var valid = false;
                    var version = 0;
                    try
                    {
                        var (valid2, id, version2, json) = await CrashReportRawParser.TryReadCrashReportDataAsync(PipeReader.Create(decompressed));
                        valid = valid2;
                        version = version2;
                    }
                    catch (Exception) { }

                    if (valid)
                    {
                        sb.AppendLine($"""
                                       UPDATE id_entity
                                       SET version = '{version}'
                                       WHERE file_id = '{entity.Id.FileId}';
                                       """);
                    }
                    else
                    {
                        sb.AppendLine($"""
                                       DELETE FROM id_entity
                                       WHERE file_id = '{entity.Id.FileId}';
                                       DELETE FROM file_entity
                                       WHERE file_id = '{entity.Id.FileId}';
                                       DELETE FROM json_entity
                                       WHERE file_id = '{entity.Id.FileId}';
                                       """);
                    }
                }
                sb.AppendLine("COMMIT;");

                await dbContext.Database.ExecuteSqlRawAsync(sb.ToString(), ct);
            }
        });
    }
}