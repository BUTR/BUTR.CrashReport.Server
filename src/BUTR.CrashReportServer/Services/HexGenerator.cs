using Microsoft.Extensions.Logging;

using System;
using System.Security.Cryptography;

namespace BUTR.CrashReportServer.Services;

public sealed class HexGenerator
{
    private readonly ILogger _logger;
    private readonly RandomNumberGenerator _random;

    public HexGenerator(ILogger<HexGenerator> logger, RandomNumberGenerator random)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    private static int GetHexChars(in ReadOnlySpan<byte> source, Span<char> buffer)
    {
        var idx1 = 0;
        var idx2 = 0;
        while (idx2 < source.Length)
        {
            var num1 = (byte) ((uint) source[idx2] >> 4);
            buffer[idx1] = num1 > (byte) 9 ? (char) ((int) num1 + 55 + 32) : (char) ((int) num1 + 48);
            var num2 = (byte) ((uint) source[idx2] & 15U);
            int num3;
            buffer[num3 = idx1 + 1] = num2 > (byte) 9 ? (char) ((int) num2 + 55 + 32) : (char) ((int) num2 + 48);
            ++idx2;
            idx1 = num3 + 1;
        }
        return idx1;
    }

    public string GetHex()
    {
        Span<byte> input = stackalloc byte[3];
        Span<char> output = stackalloc char[6];
        _random.GetBytes(input);
        GetHexChars(input, output);
        for (var i = 0; i < output.Length; i++)
            output[i] = char.ToUpper(output[i]);
        return output.ToString();
    }
}