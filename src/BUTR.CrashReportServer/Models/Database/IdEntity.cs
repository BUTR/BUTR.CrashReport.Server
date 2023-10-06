using System;

namespace BUTR.CrashReportServer.Models.Database;

public sealed record IdEntity : IEntity
{
    public required string FileId { get; set; }
    public required Guid CrashReportId { get; set; }
    public required byte Version { get; set; }
    public required DateTime Created { get; set; }
}