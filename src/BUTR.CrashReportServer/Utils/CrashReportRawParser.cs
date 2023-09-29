using BUTR.CrashReport.Models;

using System;
using System.Buffers;
using System.Buffers.Text;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BUTR.CrashReportServer.Utils;

public static class CrashReportRawParser
{
    private static readonly byte[] Newline = "\n"u8.ToArray();
    private static readonly byte[] ReportMarkerStart = "<report id='"u8.ToArray();
    private static readonly byte[] ReportIdMarkerEnd = "' "u8.ToArray();
    private static readonly byte[] ReportIdNoVersionMarkerEnd = "'>"u8.ToArray();
    private static readonly byte[] ReportVersionMarkerStart = "' version='"u8.ToArray();
    private static readonly byte[] ReportVersionMarkerEnd = "' />"u8.ToArray();
    private static readonly byte[] JsonModelMarkerStart = "<div id='json-model-data' class='headers-container'>"u8.ToArray();
    private static readonly byte[] JsonModelMarkerEnd = "</div>"u8.ToArray();

    public static async Task<(bool, Guid, byte, CrashReportModel?)> TryReadCrashReportDataAsync(PipeReader reader)
    {
        var crashRreportId = Guid.Empty;
        var crashRreportVersion = (byte) 0;
        var crashReportModel = default(CrashReportModel);

        while (true)
        {
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;

            while (!result.IsCompleted && !TryReadCrashReportData(ref buffer, out crashRreportId, out crashRreportVersion))
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
            var result = await reader.ReadAsync();
            if (!TryParse(reader, ref result, ref hasHitStartMarker, ref hasHitEndMarker, out crashReportModel))
                continue;

            if (result.IsCompleted || crashReportModel is not null)
                break;
        }

        var isValid = crashRreportId != Guid.Empty &&
                      ((crashRreportVersion > 12 && crashReportModel is not null) || (crashRreportVersion <= 12 && crashReportModel is null));

        return (isValid, crashRreportId, crashRreportVersion, crashReportModel);
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
        if (line.Slice(idxIdStart + ReportMarkerStart.Length).IndexOf(ReportIdNoVersionMarkerEnd) is var idxNoVersionStart and not -1)
        {
            if (!Utf8Parser.TryParse(line.Slice(idxIdStart + ReportMarkerStart.Length, idxNoVersionStart), out crashReportId, out _)) return false;

            version = 1;
            return true;
        }

        if (line.Slice(idxIdStart + ReportMarkerStart.Length).IndexOf(ReportIdMarkerEnd) is not (var idxIdEnd and not -1)) return false;

        if (!Utf8Parser.TryParse(line.Slice(idxIdStart + ReportMarkerStart.Length, idxIdEnd), out crashReportId, out _)) return false;

        if (line.IndexOf(ReportVersionMarkerStart) is not (var idxVersionStart and not -1)) return false;
        if (line.Slice(idxVersionStart + ReportVersionMarkerStart.Length).IndexOf(ReportVersionMarkerEnd) is not (var idxVersionEnd and not -1)) return false;

        if (!Utf8Parser.TryParse(line.Slice(idxVersionStart + ReportVersionMarkerStart.Length, idxVersionEnd), out version, out _)) return false;

        return true;
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