using System;
using System.Security.Cryptography;
using System.Text;

namespace BUTR.CrashReport.Server.Services;

/// <summary>
/// Generates the opaque delete token handed to the uploader and the SHA512 hash that is persisted.
/// The raw token is never stored - only its hash, so possession of the token is the sole proof of ownership.
/// </summary>
public static class DeleteTokenGenerator
{
    /// <summary>
    /// Response header carrying the ready-to-use delete URL (the report URL with the <c>?delete=</c> token appended).
    /// </summary>
    public const string HeaderName = "X-Delete-Url";

    /// <summary>
    /// Query string parameter carrying the delete token.
    /// </summary>
    public const string QueryName = "delete";

    public static string Generate() => Guid.NewGuid().ToString("N");

    public static byte[] ComputeHash(string token) => SHA512.HashData(Encoding.UTF8.GetBytes(token));
}
