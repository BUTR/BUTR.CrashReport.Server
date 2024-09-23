using OpenTelemetry.Exporter;

namespace BUTR.CrashReportServer.Options;

public sealed record OtlpOptions
{
    public required string LoggingEndpoint { get; init; } = default!;
    public required OtlpExportProtocol LoggingProtocol { get; init; } = default!;
    public required string TracingEndpoint { get; init; } = default!;
    public required OtlpExportProtocol TracingProtocol { get; init; } = default!;
    public required string MetricsEndpoint { get; init; } = default!;
    public required OtlpExportProtocol MetricsProtocol { get; init; } = default!;
}