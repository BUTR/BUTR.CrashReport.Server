/*
using BUTR.CrashReport.Models;

using SQLitePCL;

using System;
using System.Buffers;
using System.Buffers.Text;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReportServer.Utils;

public static class CrashReportRawParser
{
    private static readonly byte[] Newline = "\n"u8.ToArray();
    private static readonly byte[] ReportMarkerStart = "<report id='"u8.ToArray();
    private static readonly byte[] ReportIdMarkerEnd = "'"u8.ToArray();
    private static readonly byte[] ReportIdNoVersionMarkerEnd = "'>"u8.ToArray();
    private static readonly byte[] ReportVersionMarkerStart = "' version='"u8.ToArray();
    private static readonly byte[] ReportVersionMarkerEnd = "'>"u8.ToArray();
    private static readonly byte[] ReportVersionMarkerEnd2 = "' >"u8.ToArray();
    private static readonly byte[] ReportVersionMarkerEnd3 = "'/>"u8.ToArray();
    private static readonly byte[] ReportVersionMarkerEnd4 = "' />"u8.ToArray();
    private static readonly byte[] JsonModelMarkerStart = "<div id='json-model-data' class='headers-container'>"u8.ToArray();
    private static readonly byte[] JsonModelMarkerEnd = "</div>"u8.ToArray();

    public static async Task<(bool, Guid, byte, CrashReportModel?)> TryReadCrashReportDataAsync(PipeReader reader, CancellationToken ct)
    {
        var crashRreportId = Guid.Empty;
        var crashReportVersion = (byte) 0;
        var crashReportModel = default(CrashReportModel);

        while (true)
        {
            var result = await reader.ReadAsync(ct);
            var buffer = result.Buffer;

            while (!result.IsCompleted && !TryReadCrashReportData(ref buffer, out crashRreportId, out crashReportVersion))
            {
                reader.AdvanceTo(buffer.Start, buffer.End);
            }

            if (result.IsCompleted || crashRreportId != Guid.Empty)
                break;
        }

        var hasHitStartMarker = false;
        var hasHitEndMarker = false;
        while (true)
        {
            var result = await reader.ReadAsync(ct);
            if (!TryParse(reader, ref result, ref hasHitStartMarker, ref hasHitEndMarker, out crashReportModel))
                continue;

            if (result.IsCompleted || crashReportModel is not null)
                break;
        }

        var isValid = crashRreportId != Guid.Empty &&
                      crashReportVersion > 0 &&
                      ((crashReportVersion > 12 && crashReportModel is not null) || (crashReportVersion <= 12 && crashReportModel is null));

        return (isValid, crashRreportId, crashReportVersion, crashReportModel);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryReadCrashReportData(ref ReadOnlySequence<byte> buffer, out Guid crashReportId, out byte version)
    {
        crashReportId = Guid.Empty;
        version = 0;

        var reader = new SequenceReader<byte>(buffer);

        if (!reader.TryReadTo(out ReadOnlySpan<byte> line, Newline)) return false;
        buffer = buffer.Slice(reader.Position);

        if (line.IndexOf(ReportMarkerStart) is not (var idxIdStart and not -1)) return false;
        var line2 = line.utf8_span_to_string();

        if (line.Slice(idxIdStart + ReportMarkerStart.Length).IndexOf(ReportIdMarkerEnd) is not (var idxIdEnd and not -1)) return false;

        if (!Utf8Parser.TryParse(line.Slice(idxIdStart + ReportMarkerStart.Length, idxIdEnd), out crashReportId, out _)) return false;

        if (line.IndexOf(ReportVersionMarkerStart) is var idxVersionStart and not -1)
        {
            var idxEnd = -1;
            if (line.Slice(idxVersionStart + ReportVersionMarkerStart.Length).IndexOf(ReportVersionMarkerEnd) is var idxVersionEnd and not -1) idxEnd = idxVersionEnd;
            if (line.Slice(idxVersionStart + ReportVersionMarkerStart.Length).IndexOf(ReportVersionMarkerEnd2) is var idxVersionEnd2 and not -1) idxEnd = idxVersionEnd2;
            if (line.Slice(idxVersionStart + ReportVersionMarkerStart.Length).IndexOf(ReportVersionMarkerEnd3) is var idxVersionEnd3 and not -1) idxEnd = idxVersionEnd3;
            if (line.Slice(idxVersionStart + ReportVersionMarkerStart.Length).IndexOf(ReportVersionMarkerEnd4) is var idxVersionEnd4 and not -1) idxEnd = idxVersionEnd4;
            if (idxEnd == -1) return false;

            if (!Utf8Parser.TryParse(line.Slice(idxVersionStart + ReportVersionMarkerStart.Length, idxEnd), out version, out _)) return false;

            return true;
        }

        if (line.Slice(idxIdStart + ReportMarkerStart.Length).IndexOf(ReportIdNoVersionMarkerEnd) is var idxNoVersionStart and not -1)
        {
            if (!Utf8Parser.TryParse(line.Slice(idxIdStart + ReportMarkerStart.Length, idxNoVersionStart), out crashReportId, out _)) return false;

            version = 1;
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParse(PipeReader reader, ref ReadResult result, ref bool hasHitStartMarker, ref bool hasHitEndMarker, out CrashReportModel? crashReportModel)
    {
        crashReportModel = default;

        if (result.IsCompleted) return true;

        var buffer = result.Buffer;
        while (true)
        {
            if (ReadLine(ref buffer) is not { IsEmpty: false } line)
            {
                reader.AdvanceTo(buffer.Start, buffer.End);
                break;
            }

            if (hasHitStartMarker)
            {
                crashReportModel = JsonSerializer.Deserialize<CrashReportModel>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    Converters = { new JsonStringEnumConverter() }
                });
                break;
            }

            if (!hasHitStartMarker) hasHitStartMarker = line.IndexOf(JsonModelMarkerStart) is not -1;
            if (!hasHitEndMarker && hasHitStartMarker) hasHitEndMarker = line.IndexOf(JsonModelMarkerEnd) is not -1;
        }

        return crashReportModel is not null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<byte> ReadLine(ref ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);

        if (reader.TryReadTo(out ReadOnlySpan<byte> line, Newline))
        {
            buffer = buffer.Slice(reader.Position);
            return line;
        }

        return default;
    }
}
*/