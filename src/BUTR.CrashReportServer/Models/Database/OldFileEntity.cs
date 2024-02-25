namespace BUTR.CrashReportServer.Models.Database;

public sealed record OldFileEntity : IEntity
{
    public int RowId { get; set; }
    public required OldIdEntity Id { get; set; }
    public required byte[] DataCompressed { get; set; }
}