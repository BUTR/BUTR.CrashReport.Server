using System;
using System.Linq;

namespace BUTR.CrashReport.Server.Services;

public class FileIdGenerator
{
    // 6 Crockford Base32 characters = 30 bits of entropy, 64x the space of the previous 6 hex chars (24 bits).
    private const int IdLength = 6;
    // Candidates handed to insert_crash_report, which tries them in order until one is free within the tenant.
    private const int Count = 300;

    private readonly Base32Generator _base32Generator;

    public FileIdGenerator(Base32Generator base32Generator)
    {
        _base32Generator = base32Generator ?? throw new ArgumentNullException(nameof(base32Generator));
    }

    public string[] Generate() => _base32Generator.GetIds(Count, IdLength).ToArray();
}