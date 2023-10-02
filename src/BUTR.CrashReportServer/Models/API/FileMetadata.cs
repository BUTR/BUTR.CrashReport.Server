using System;

namespace BUTR.CrashReportServer.Models.API;

public sealed record FileMetadata(string File, byte Version, DateTimeOffset Date);