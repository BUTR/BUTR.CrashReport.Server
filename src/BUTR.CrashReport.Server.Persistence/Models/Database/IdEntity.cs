using System;

namespace BUTR.CrashReport.Server.Models.Database;

public sealed record IdEntity : IEntity
{
    public required Guid CrashReportId { get; set; }
    public required string FileId { get; set; }

    public ReportEntity? Report { get; set; }
}