namespace BUTR.CrashReport.Server.Models.Database;

public sealed class CrashReportInsertResult
{
    public required string FileId { get; init; }
    public required byte Tenant { get; init; }
    public required bool Created { get; init; }
}