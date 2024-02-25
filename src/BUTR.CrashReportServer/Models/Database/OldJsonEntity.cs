namespace BUTR.CrashReportServer.Models.Database;

public sealed record OldJsonEntity : IEntity
{
    public int RowId { get; set; }
    public required OldIdEntity Id { get; set; }
    public required byte[] CrashReportCompressed { get; set; }
}