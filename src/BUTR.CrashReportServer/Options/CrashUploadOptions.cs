namespace BUTR.CrashReportServer.Options;

public record CrashUploadOptions
{
    public int MinContentLength { get; set; }
    public int MaxContentLength { get; set; }

    public string? BaseUri { get; set; }
}

public record ReportOptions
{
    public string? BaseUri { get; set; }
}