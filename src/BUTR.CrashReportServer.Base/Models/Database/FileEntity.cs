namespace BUTR.CrashReportServer.Models.Database;

public sealed record FileEntity : IEntity
{
    public required string FileId { get; set; }
    public IdEntity? Id { get; set; }
    public required byte[] DataCompressed { get; set; }
}