using System;

namespace BUTR.CrashReport.Server.Models.Database;

public sealed class CompressionDictionaryEntity : IEntity
{
    public short Id { get; set; }

    public required byte Tenant { get; set; }

    public required CompressionDictionaryKind Kind { get; set; }

    public required byte Version { get; set; }

    public required byte[] Bytes { get; set; }

    public required DateTime Created { get; set; }

    public required bool IsActive { get; set; }
}

public enum CompressionDictionaryKind : byte
{
    Json = 0,
    Html = 1,
}