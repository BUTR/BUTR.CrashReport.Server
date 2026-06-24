using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace BUTR.CrashReport.Server.Services;

/// <summary>
/// Generates identifiers using Douglas Crockford's Base32 alphabet, which omits the visually
/// ambiguous letters I, L, O and U (I/L read as 1, O as 0, U is dropped to avoid accidental words).
/// Backed by a CSPRNG so ids are not predictable.
/// </summary>
public sealed class Base32Generator
{
    /// <summary>Crockford Base32 alphabet: digits 0-9 then A-Z without I, L, O, U.</summary>
    public const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private readonly RandomNumberGenerator _random;

    public Base32Generator(RandomNumberGenerator random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <summary>
    /// Whether <paramref name="c"/> is part of the alphabet. Case-sensitive (ids are stored uppercase),
    /// and because hex digits are a subset of the alphabet, previously generated hex ids remain valid.
    /// </summary>
    public static bool IsValidChar(char c) => Alphabet.IndexOf(c) >= 0;

    /// <summary>
    /// Returns up to <paramref name="maxCount"/> distinct random ids, each <paramref name="charLength"/> characters long
    /// (5 bits of entropy per character).
    /// </summary>
    public HashSet<string> GetIds(int maxCount, int charLength)
    {
        Span<byte> input = stackalloc byte[maxCount * charLength];
        _random.GetBytes(input);

        // One random byte feeds one character. (byte & 0x1F) is uniform over 0..31 because 256 is a multiple of 32.
        Span<char> output = stackalloc char[charLength];
        var unique = new HashSet<string>(maxCount);
        for (var i = 0; i < maxCount; i++)
        {
            var slice = input.Slice(i * charLength, charLength);
            for (var j = 0; j < charLength; j++)
                output[j] = Alphabet[slice[j] & 0x1F];
            unique.Add(output.ToString());
        }
        return unique;
    }
}
