using BUTR.CrashReport.Server.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Net.Http.Headers;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReport.Server;

/// <summary>
/// Output caching policy for the public, read-only crash report endpoints.
/// Stored reports are immutable once uploaded, so they are highly cacheable; the matching
/// <see cref="ReportTag"/> is evicted when a report is deleted so a removed report is never
/// served from the cache.
/// </summary>
public sealed class ReportOutputCachePolicy : IOutputCachePolicy
{
    // Reports never change after upload; a delete evicts the tag explicitly, so the TTL only
    // bounds the window for the (rare) case where an eviction is missed.
    private static readonly TimeSpan Expiration = TimeSpan.FromMinutes(5);

    public static string ReportTag(byte tenant, string fileId) => $"report:{tenant}:{fileId}";

    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        var request = context.HttpContext.Request;

        // Only cache plain GETs of a report. The delete-confirmation flow (the `delete` query token)
        // is per-user and must never be cached.
        var cacheable = HttpMethods.IsGet(request.Method) && !request.Query.ContainsKey(DeleteTokenGenerator.QueryName);

        context.EnableOutputCaching = cacheable;
        context.AllowCacheLookup = cacheable;
        context.AllowCacheStorage = cacheable;
        context.AllowLocking = true;
        context.ResponseExpirationTimeSpan = Expiration;

        // The same report can be returned as html or json (Accept) and gzip-compressed or not
        // (Accept-Encoding), so the cache key must vary by both to avoid serving the wrong body.
        context.CacheVaryByRules.HeaderNames = new[] { HeaderNames.Accept, HeaderNames.AcceptEncoding };

        if (cacheable && TryGetReportTag(request, out var tag))
            context.Tags.Add(tag);

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        // Never cache 404/400 etc. - only successful report responses.
        if (context.HttpContext.Response.StatusCode != StatusCodes.Status200OK)
            context.AllowCacheStorage = false;

        return ValueTask.CompletedTask;
    }

    private static bool TryGetReportTag(HttpRequest request, out string tag)
    {
        tag = string.Empty;

        if (request.RouteValues.TryGetValue("filename", out var filenameValue) && filenameValue?.ToString() is { Length: > 0 } filename)
        {
            // Routes pass the file id with or without an extension (.html/.json); normalize to the bare id.
            filename = Path.GetFileNameWithoutExtension(filename);
            var tenant = request.RouteValues.TryGetValue("tenant", out var tenantValue) && byte.TryParse(tenantValue?.ToString(), out var t) ? t : (byte) 1;
            tag = ReportTag(tenant, filename);
            return true;
        }

        return false;
    }
}
