namespace BUTR.CrashReportServer.Models.Database;

public sealed record OldJsonEntity : IEntity
{
    public required IdEntity Id { get; set; }
    public required byte[] CrashReportCompressed { get; set; }
}