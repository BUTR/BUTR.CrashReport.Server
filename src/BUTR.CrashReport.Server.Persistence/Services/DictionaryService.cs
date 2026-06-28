using BUTR.CrashReport.Server.Contexts;
using BUTR.CrashReport.Server.Models.Database;

using Microsoft.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ZstdSharp;

namespace BUTR.CrashReport.Server.Services;

public sealed class DictionaryService
{
    public sealed record DictionaryInfo(short Id, byte Tenant, CompressionDictionaryKind Kind, byte Version, DateTime Created, bool IsActive, int SizeBytes);

    /// <param name="ReplacedId">The dictionary that was active for this key before, now demoted (null if there was none).</param>
    public sealed record SetResult(short Id, byte Tenant, CompressionDictionaryKind Kind, byte Version, int SizeBytes, short? ReplacedId);

    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public DictionaryService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public async Task<SetResult> SetActiveAsync(byte tenant, CompressionDictionaryKind kind, byte version, byte[] bytes, CancellationToken ct)
    {
        if (bytes is null || bytes.Length == 0)
            throw new InvalidOperationException("Dictionary is empty.");
        ValidateDictionary(bytes);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);

        var replaced = await dbContext.CompressionDictionaries
            .Where(x => x.Tenant == tenant && x.Kind == kind && x.Version == version && x.IsActive)
            .Select(x => (short?) x.Id)
            .FirstOrDefaultAsync(ct);

        if (replaced is not null)
            await dbContext.CompressionDictionaries
                .Where(x => x.Tenant == tenant && x.Kind == kind && x.Version == version && x.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);

        var entity = new CompressionDictionaryEntity
        {
            Tenant = tenant,
            Kind = kind,
            Version = version,
            Bytes = bytes,
            Created = DateTime.UtcNow,
            IsActive = true,
        };
        dbContext.CompressionDictionaries.Add(entity);
        await dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new SetResult(entity.Id, tenant, kind, version, bytes.Length, replaced);
    }

    public async Task ActivateAsync(short id, CancellationToken ct)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        var dict = await dbContext.CompressionDictionaries.FirstOrDefaultAsync(x => x.Id == id, ct)
                   ?? throw new InvalidOperationException($"Dictionary {id} not found.");
        if (dict.IsActive) return;

        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);
        await dbContext.CompressionDictionaries
            .Where(x => x.Tenant == dict.Tenant && x.Kind == dict.Kind && x.Version == dict.Version && x.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
        await dbContext.CompressionDictionaries
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true), ct);
        await tx.CommitAsync(ct);
    }

    public async Task<bool> DeleteAsync(short id, CancellationToken ct)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        try
        {
            var deleted = await dbContext.CompressionDictionaries.Where(x => x.Id == id && !x.IsActive).ExecuteDeleteAsync(ct);
            return deleted > 0;
        }
        catch (DbUpdateException)
        {
            return false;
        }
        catch (Exception ex) when (ex.GetType().Name == "PostgresException")
        {
            return false; // foreign_key_violation - dictionary is in use
        }
    }

    public async Task<IReadOnlyList<DictionaryInfo>> ListAsync(byte? tenant, CancellationToken ct)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        return await dbContext.CompressionDictionaries
            .Where(x => tenant == null || x.Tenant == tenant)
            .OrderBy(x => x.Tenant).ThenBy(x => x.Kind).ThenBy(x => x.Version).ThenBy(x => x.Id)
            .Select(x => new DictionaryInfo(x.Id, x.Tenant, x.Kind, x.Version, x.Created, x.IsActive, x.Bytes.Length))
            .ToListAsync(ct);
    }

    private static void ValidateDictionary(byte[] dict)
    {
        try
        {
            using var compressor = new Compressor();
            compressor.LoadDictionary(dict);
            using var decompressor = new Decompressor();
            decompressor.LoadDictionary(dict);
            var probe = Encoding.UTF8.GetBytes("compression dictionary validation probe");
            var round = decompressor.Unwrap(compressor.Wrap(probe).ToArray()).ToArray();
            if (!round.AsSpan().SequenceEqual(probe))
                throw new InvalidOperationException("dictionary failed a compress/decompress round-trip");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Not a usable zstd dictionary: {ex.Message}");
        }
    }
}