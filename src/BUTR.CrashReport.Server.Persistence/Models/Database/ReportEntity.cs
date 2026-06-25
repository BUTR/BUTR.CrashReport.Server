using System;

namespace BUTR.CrashReport.Server.Models.Database;

public sealed record ReportEntity : IEntity
{
    public required Guid CrashReportId { get; set; }
    public required byte Tenant { get; set; }
    public required byte Version { get; set; }
    public required DateTime Created { get; set; }

    /// <summary>
    /// SHA512 hash of the delete token returned to the uploader. The raw token is never stored.
    /// Null for reports uploaded before delete tokens were introduced.
    /// </summary>
    public byte[]? DeleteTokenHash { get; set; }

    public IdEntity? Id { get; set; }
    public HtmlEntity? Html { get; set; }
    public JsonEntity? Json { get; set; }
    public OldHtmlEntity? OldHtml { get; set; }
}