namespace BUTR.CrashReport.Server.Models.Database;

public sealed record JsonEntity : IEntity
{
    public required string FileId { get; set; }
    public IdEntity? Id { get; set; }
    public required string CrashReport { get; set; }
}