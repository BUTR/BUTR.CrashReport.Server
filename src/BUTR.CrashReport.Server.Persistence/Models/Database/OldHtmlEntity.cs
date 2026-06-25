using System;

namespace BUTR.CrashReport.Server.Models.Database;

/// <summary>
/// Archive of legacy HTML that was converted to JSON by the one-off migration. Retained so the conversion is
/// reversible/auditable; can be purged in bulk once no longer needed. Never served.
/// </summary>
public sealed record OldHtmlEntity : IEntity
{
    public required Guid CrashReportId { get; set; }
    public required byte[] DataCompressed { get; set; }

    public ReportEntity? Report { get; set; }
}
