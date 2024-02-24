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
        
        var idDataCount = sqlite.Set<IdEntity>().Count();
        for (var i = 0; i < idDataCount % take; i+= take)
        {
            var data = await sqlite.Set<IdEntity>().OrderBy(x => x.FileId).Skip(i * take).Take(take).AsAsyncEnumerable()
                .Select(x => x with
                {
                    Created = DateTime.SpecifyKind(x.Created, DateTimeKind.Utc)
                }).ToArrayAsync(ct);
            await postgres.Set<IdEntity>().AddRangeAsync(data, ct);
            await postgres.SaveChangesAsync(ct);
        }
        
        var fileDataCount = sqlite.Set<FileEntity>().Count();
        for (var i = 0; i < fileDataCount % take; i+= take)
        {
            var data = await sqlite.Set<FileEntity>().OrderBy(x => x.Id.FileId).Skip(i * take).Take(take).ToArrayAsync(ct);
            await postgres.Set<FileEntity>().AddRangeAsync(data, ct);
            await postgres.SaveChangesAsync(ct);
        }
        
        var jsonDataCount = sqlite.Set<OldJsonEntity>().Count();
        for (var i = 0; i < jsonDataCount % take; i+= take)
        {
            var data = await sqlite.Set<OldJsonEntity>().OrderBy(x => x.Id.FileId).Skip(i * take).Take(take).AsAsyncEnumerable()
                .SelectAwait(async x => new JsonEntity
                {
                    Id = x.Id,
                    CrashReport = await new StreamReader(await _compressor.DecompressAsync(x.CrashReportCompressed, ct)).ReadToEndAsync(ct),
                }).ToArrayAsync(ct);
            await postgres.Set<JsonEntity>().AddRangeAsync(data, ct);
            await postgres.SaveChangesAsync(ct);
        }
    }
}