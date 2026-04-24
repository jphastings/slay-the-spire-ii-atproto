using System;
using System.Numerics;

namespace AtprotoTracker.Signing;

internal static class Base58Btc
{
    private const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    private static readonly int[] Indices = BuildIndices();
    private static int[] BuildIndices()
    {
        var table = new int[128];
        for (var i = 0; i < table.Length; i++) table[i] = -1;
        for (var i = 0; i < Alphabet.Length; i++) table[Alphabet[i]] = i;
        return table;
    }

    public static string Encode(ReadOnlySpan<byte> input)
    {
        var leadingZeros = 0;
        while (leadingZeros < input.Length && input[leadingZeros] == 0) leadingZeros++;

        // BigInteger ctor wants big-endian unsigned bytes.
        var num = new BigInteger(input, isUnsigned: true, isBigEndian: true);

        var digits = new System.Collections.Generic.List<char>(input.Length * 2);
        while (num > 0)
        {
            num = BigInteger.DivRem(num, 58, out var rem);
            digits.Add(Alphabet[(int)rem]);
        }
        for (var i = 0; i < leadingZeros; i++) digits.Add('1');
        digits.Reverse();
        return new string(digits.ToArray());
    }

    public static byte[] Decode(string input)
    {
        // Leading '1' characters represent leading zero bytes.
        var leadingZeros = 0;
        while (leadingZeros < input.Length && input[leadingZeros] == '1') leadingZeros++;

        var num = BigInteger.Zero;
        for (var i = leadingZeros; i < input.Length; i++)
        {
            var c = input[i];
            if (c >= Indices.Length || Indices[c] < 0)
                throw new FormatException($"invalid base58btc character '{c}'");
            num = num * 58 + Indices[c];
        }

        // BigInteger.ToByteArray returns little-endian, possibly with a trailing
        // 0x00 sign byte. We want big-endian bytes with no sign byte.
        var leBytes = num.ToByteArray();
        var end = leBytes.Length;
        if (end > 1 && leBytes[end - 1] == 0) end--;

        var result = new byte[leadingZeros + end];
        for (var i = 0; i < end; i++) result[leadingZeros + end - 1 - i] = leBytes[i];
        return result;
    }
}
