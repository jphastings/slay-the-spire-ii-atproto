using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace AtprotoTracker;

internal static class Tid
{
    private const string Alphabet = "234567abcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// Build a deterministic atproto TID from a run's start time and seed.
    /// Layout (64 bits): bit 0 = 0 (reserved), bits 1-53 = microseconds since epoch,
    /// bits 54-63 = top 10 bits of MD5(seed).
    /// Encoded as 13-char base32-sortable string.
    /// </summary>
    public static string FromRun(long startTimeUnixSeconds, uint gameSeed)
    {
        Span<byte> seedBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(seedBytes, gameSeed);
        Span<byte> hash = stackalloc byte[16];
        MD5.HashData(seedBytes, hash);
        int rand = (hash[0] << 2) | (hash[1] >> 6);

        long microseconds = startTimeUnixSeconds * 1_000_000L;
        long tidValue = (microseconds << 10) | (uint)rand;

        var buf = new char[13];
        for (int i = 12; i >= 0; i--)
        {
            buf[i] = Alphabet[(int)(tidValue & 0x1F)];
            tidValue >>= 5;
        }
        return new string(buf);
    }
}
