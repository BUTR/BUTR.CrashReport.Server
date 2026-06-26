using System;

namespace BUTR.CrashReport.Server.Models.Database;

/// <summary>
/// An additional public file id that resolves to a report whose canonical id is <see cref="ReportEntity.FileId"/>.
/// These come from legacy repeat uploads (each upload used to mint a new file id); kept so old URLs keep working.
/// No new rows are created at runtime - new uploads are idempotent and reuse the canonical id.
/// </summary>
public sealed class IdAliasEntity : IEntity
{
    public required string FileId { get; set; }
    public required Guid CrashReportId { get; set; }
    public required byte Tenant { get; set; }

    public ReportEntity? Report { get; set; }
}
