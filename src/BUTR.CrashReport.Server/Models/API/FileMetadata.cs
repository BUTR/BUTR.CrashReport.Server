using System;

namespace BUTR.CrashReport.Server.Models.API;

public sealed record FileMetadata(string File, Guid Id, byte Version, DateTimeOffset Date);