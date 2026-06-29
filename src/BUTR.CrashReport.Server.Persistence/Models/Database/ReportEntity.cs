using System;
using System.Collections.Generic;

namespace BUTR.CrashReport.Server.Models.Database;

public sealed class ReportEntity : IEntity
{
    public required Guid CrashReportId { get; set; }
    public required byte Tenant { get; set; }
    public required byte Version { get; set; }
    public required DateTime Created { get; set; }

    /// <summary>
    /// The canonical public file id of this report (one per report). Unique within a <see cref="Tenant"/>.
    /// Additional file ids that resolve to the same report (legacy repeat uploads) live in <see cref="Aliases"/>.
    /// </summary>
    public required string FileId { get; set; }

    /// <summary>
    /// SHA512 hash of the delete token returned to the uploader. The raw token is never stored.
    /// Null for reports uploaded before delete tokens were introduced.
    /// </summary>
    public byte[]? DeleteTokenHash { get; set; }

    public ICollection<IdAliasEntity> Aliases { get; set; } = new List<IdAliasEntity>();
    public HtmlEntity? Html { get; set; }
    public JsonEntity? Json { get; set; }
    public OldHtmlEntity? OldHtml { get; set; }
}