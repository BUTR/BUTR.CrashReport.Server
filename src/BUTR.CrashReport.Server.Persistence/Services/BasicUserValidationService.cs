using AspNetCore.Authentication.Basic;

using BUTR.CrashReport.Server.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BUTR.CrashReport.Server.Services;

public sealed class BasicUserValidationService : IBasicUserValidationService
{
    /// <summary>
    /// Compares two strings in constant time. The values are hashed first so the comparison length
    /// is fixed and the secret's length is not leaked through timing.
    /// </summary>
    private static bool FixedTimeEquals(string a, string b)
    {
        Span<byte> hashA = stackalloc byte[SHA256.HashSizeInBytes];
        Span<byte> hashB = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(a), hashA);
        SHA256.HashData(Encoding.UTF8.GetBytes(b), hashB);
        return CryptographicOperations.FixedTimeEquals(hashA, hashB);
    }

    private readonly ILogger _logger;
    private readonly AuthOptions _options;

    public BasicUserValidationService(ILogger<BasicUserValidationService> logger, IOptionsSnapshot<AuthOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<bool> IsValidAsync(string username, string password)
    {
        // Use & rather than && so both comparisons always run - a short-circuit would leak whether the username matched.
        var isValid = FixedTimeEquals(_options.Username ?? string.Empty, username);
        isValid &= FixedTimeEquals(_options.Password ?? string.Empty, password);
        return Task.FromResult(isValid);
    }
}