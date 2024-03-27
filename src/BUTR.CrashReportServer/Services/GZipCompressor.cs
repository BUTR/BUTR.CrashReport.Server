using Microsoft.IO;

using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReportServer.Services;

public sealed class GZipCompressor
{
    private readonly RecyclableMemoryStreamManager _streamManager;

    public GZipCompressor(RecyclableMemoryStreamManager streamManager)
    {
        _streamManager = streamManager ?? throw new ArgumentNullException(nameof(streamManager));
    }

    public async Task<MemoryStream> CompressAsync(byte[] data, CancellationToken ct)
    {
        await using var decompressedStream = _streamManager.GetStream(data);
        return await CompressAsync(decompressedStream, ct);
    }
    public async Task<MemoryStream> CompressAsync(Stream decompressedStream, CancellationToken ct)
    {
        if (decompressedStream.CanSeek) decompressedStream.Seek(0, SeekOrigin.Begin);
        var compressedStream = _streamManager.GetStream();
        await using var zipStream = new GZipStream(compressedStream, CompressionMode.Compress, true);
        await decompressedStream.CopyToAsync(zipStream, ct);
        return compressedStream;
    }

    public async Task<MemoryStream> DecompressAsync(byte[] data, CancellationToken ct)
    {
        await using var compressedStream = _streamManager.GetStream(data);
        return await DecompressAsync(compressedStream, ct);
    }
    public async Task<MemoryStream> DecompressAsync(Stream compressedStream, CancellationToken ct)
    {
        if (compressedStream.CanSeek) compressedStream.Seek(0, SeekOrigin.Begin);
        var decompressedStream = _streamManager.GetStream();
        await using var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress, true);
        await zipStream.CopyToAsync(decompressedStream, ct);
        decompressedStream.Seek(0, SeekOrigin.Begin);
        return decompressedStream;
    }
}