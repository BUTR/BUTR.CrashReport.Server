using BUTR.CrashReportServer.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

        public async Task<string?> GenerateUniqueFilePath(CancellationToken ct)
        {
            try
            {
                await _lock.WaitAsync(ct);
                while (!ct.IsCancellationRequested)
                {
                    var filePath = Path.GetFullPath(Path.Combine(_options.Path ?? string.Empty, $"{_random.Next():X2}.html"));
                    if (!File.Exists(filePath))
                        return filePath;
                }
                return null;
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}