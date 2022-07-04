using BUTR.CrashReportServer.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Numeral;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReportServer.Services
{
    public class SemaphoreFilePathProvider : IFilePathProvider
    {
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _lock = new(1);
        private readonly Random _random = new();
        private StorageOptions _options;

        public SemaphoreFilePathProvider(ILogger<SemaphoreFilePathProvider> logger, IOptionsMonitor<StorageOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options.CurrentValue ?? throw new ArgumentNullException(nameof(options));
            options.OnChange(OnChange);
        }

        private void OnChange(StorageOptions options)
        {
            _options = options;
        }

        private string? GetHex(CancellationToken ct)
        {
            Span<byte> buffer = stackalloc byte[5];
            Span<char> buffer2 = stackalloc char[10];
            while (!ct.IsCancellationRequested)
            {
                _random.NextBytes(buffer);
                HexConverter.GetChars(buffer, buffer2);
                for (var i = 0; i < buffer2.Length; i++)
                    buffer2[i] = char.ToUpper(buffer2[i]);
                
                var filePath = Path.GetFullPath(Path.Combine(_options.Path ?? string.Empty, $"{buffer2}.html"));
                if (!File.Exists(filePath))
                    return filePath;
            }
            return null;
        }
        
        public async Task<string?> GenerateUniqueFilePath(CancellationToken ct)
        {
            try
            {
                await _lock.WaitAsync(ct);
                return GetHex(ct);
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}