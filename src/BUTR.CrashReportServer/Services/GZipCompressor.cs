using Microsoft.IO;

using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReportServer.Services
{
    public sealed class GZipCompressor
    {
        private readonly RecyclableMemoryStreamManager _streamManager;

        public GZipCompressor(RecyclableMemoryStreamManager streamManager)
        {
            _streamManager = streamManager ?? throw new ArgumentNullException(nameof(streamManager));
        }

        public async Task<MemoryStream> CompressAsync(Stream stream, CancellationToken ct)
        {
            var compressedStream = _streamManager.GetStream();
            await using var zipStream = new GZipStream(compressedStream, CompressionMode.Compress, true);
            await stream.CopyToAsync(zipStream, ct);
            return compressedStream;
        }
    }
}