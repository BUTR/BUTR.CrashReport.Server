using BUTR.CrashReportServer.Contexts;
using BUTR.CrashReportServer.Models.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReportServer.Services;

public sealed class DatabaseMigrator : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GZipCompressor _compressor;

    public DatabaseMigrator(IServiceScopeFactory scopeFactory, GZipCompressor compressor)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _compressor = compressor ?? throw new ArgumentNullException(nameof(compressor));
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var dbContextFactoryOld = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OldAppDbContext>>();

        var postgres = await dbContextFactory.CreateDbContextAsync(ct);
        var sqlite = await dbContextFactoryOld.CreateDbContextAsync(ct);

        const int take = 10000;
        
        var idDataCount = sqlite.Set<OldIdEntity>().AsNoTracking().Count();
        var idDataIterations = (idDataCount / take) + 1;
        for (var i = 0; i < idDataIterations; i++)
        {
            var data = await sqlite.Set<OldIdEntity>().AsNoTracking().OrderBy(x => x.RowId).Skip(i * take).Take(take).AsAsyncEnumerable()
                .Select(x => new IdEntity
                {
                    FileId = x.FileId,
                    CrashReportId = x.CrashReportId,
                    Version = x.Version,
                    Created = DateTime.SpecifyKind(x.Created, DateTimeKind.Utc)
                }).ToArrayAsync(ct);
            await postgres.Set<IdEntity>().AddRangeAsync(data, ct);
            await postgres.SaveChangesAsync(ct);
        }
        
        var fileDataCount = sqlite.FileEntities.AsNoTracking().Count();
        var fileDataIterations = (fileDataCount / take) + 1;
        for (var i = 0; i < fileDataIterations; i++)
        {
            var data = await sqlite.FileEntities.AsNoTracking().OrderBy(x => x.RowId).Skip(i * take).Take(take).AsAsyncEnumerable()
                .Select(x => new FileEntity
                {
                    FileId = x.Id.FileId,
                    DataCompressed = x.DataCompressed,
                })
                .ToArrayAsync(ct);
            await postgres.FileEntities.AddRangeAsync(data, ct);
            await postgres.SaveChangesAsync(ct);
        }
        
        var jsonDataCount = sqlite.JsonEntities.AsNoTracking().Count();
        var jsonDataIterations = (jsonDataCount / take) + 1;
        for (var i = 0; i < jsonDataIterations; i++)
        {
            var data = await sqlite.JsonEntities.OrderBy(x => x.RowId).Skip(i * take).Take(take).AsAsyncEnumerable()
                .SelectAwait(async x => new JsonEntity
                {
                    FileId = x.Id.FileId,
                    CrashReport = await new StreamReader(await _compressor.DecompressAsync(x.CrashReportCompressed, ct)).ReadToEndAsync(ct),
                }).ToArrayAsync(ct);
            await postgres.JsonEntities.AddRangeAsync(data, ct);
            await postgres.SaveChangesAsync(ct);
        }
    }
}