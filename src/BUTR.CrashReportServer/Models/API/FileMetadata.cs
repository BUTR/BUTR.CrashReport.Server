using System;

namespace BUTR.CrashReportServer.Models.API;

public sealed record FileMetadata(string File, Guid Id, byte Version, DateTimeOffset Date);