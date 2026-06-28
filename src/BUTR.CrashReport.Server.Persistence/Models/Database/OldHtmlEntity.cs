using System;

namespace BUTR.CrashReport.Server.Models.Database;

public sealed class OldHtmlEntity : IEntity
{
    public required Guid CrashReportId { get; set; }

    public required byte[] DataCompressed { get; set; }

    public short? DictId { get; set; }

    public ReportEntity? Report { get; set; }
    public CompressionDictionaryEntity? Dictionary { get; set; }
}