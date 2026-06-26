using System;

namespace BUTR.CrashReport.Server.Models.Database;

public sealed class HtmlEntity : IEntity
{
    public required Guid CrashReportId { get; set; }
    public required byte[] DataCompressed { get; set; }

    public ReportEntity? Report { get; set; }
}