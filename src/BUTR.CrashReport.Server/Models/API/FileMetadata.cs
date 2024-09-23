using System;

namespace BUTR.CrashReport.Server.Models.API;

public sealed record FileMetadata
{
    public string File { get; init; }
    public Guid Id { get; init; }
    public byte Version { get; init; }
    public DateTimeOffset Date { get; init; }
}