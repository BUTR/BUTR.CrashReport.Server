namespace BUTR.CrashReportServer.Models.Database;

public sealed record JsonEntity : IEntity
{
    public required IdEntity Id { get; set; }
    public required byte[] CrashReportCompressed { get; set; }
}