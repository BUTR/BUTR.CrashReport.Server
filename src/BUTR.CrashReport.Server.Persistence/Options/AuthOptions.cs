namespace BUTR.CrashReport.Server.Options;

public record AuthOptions
{
    public string? Username { get; set; }
    public string? Password { get; set; }
}