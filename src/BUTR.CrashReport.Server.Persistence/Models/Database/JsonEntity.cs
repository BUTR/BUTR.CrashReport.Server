using System;

namespace BUTR.CrashReport.Server.Models.Database;

public sealed class JsonEntity : IEntity
{
    public required Guid CrashReportId { get; set; }
    public required string Json { get; set; }

    public ReportEntity? Report { get; set; }
}