using Microsoft.Extensions.Diagnostics.HealthChecks;

using System;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReport.Server.Services;

public sealed class UptimeKumaHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly HttpClient _httpClient;

    public UptimeKumaHealthCheckPublisher(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task PublishAsync(HealthReport report, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync($"?status={(report.Status == HealthStatus.Healthy ? "up" : "down")}&msg={Uri.EscapeDataString(report.Status.ToString())}&ping={report.TotalDuration.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)}", ct);
        response.EnsureSuccessStatusCode();
    }
}