using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace BUTR.CrashReportServer.Services;

public sealed class HexGenerator
{
    private readonly RandomNumberGenerator _random;

    public HexGenerator(RandomNumberGenerator random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    private static int GenerateHexChars(in ReadOnlySpan<byte> source, Span<char> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            var char1 = source[i] >> 4;
            destination[i * 2] = char.ToUpperInvariant(char1 > 9 ? (char) (char1 + 55 + 32) : (char) (char1 + 48));
            var char2 = source[i] & 15;
            destination[i * 2 + 1] = char.ToUpperInvariant(char2 > 9 ? (char) (char2 + 55 + 32) : (char) (char2 + 48));
        }
        return source.Length * 2;
    }

    public HashSet<string> GetHex(int maxCount, int length)
    {
        var charLength = length * 2;
        var byteLength = length;

        Span<char> output = stackalloc char[maxCount * charLength];
        Span<byte> input = stackalloc byte[maxCount * byteLength];
        _random.GetBytes(input);

        var unique = new HashSet<string>(maxCount);
        GenerateHexChars(input, output);
        for (var i = 0; i < output.Length; i += charLength)
            unique.Add(output.Slice(i, charLength).ToString());
        return unique;
    }
}