using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AtprotoTracker.Signing;

/// <summary>
/// Minimal canonical DAG-CBOR encoder for the subset of JsonNode values the
/// run record uses: objects, arrays, strings, 64-bit integers, booleans, null,
/// and (for safety) 64-bit floats.
///
/// DAG-CBOR canonical rules enforced:
/// - Definite-length strings/arrays/maps only.
/// - Map keys sorted by UTF-8 byte sequence (lexicographic).
/// - Integers in shortest form.
/// - Floats always 8-byte IEEE 754.
/// - No CBOR tags (we never emit CID links from the mod).
/// </summary>
internal static class DagCbor
{
    public static byte[] Encode(JsonNode node)
    {
        using var ms = new MemoryStream();
        WriteNode(ms, node);
        return ms.ToArray();
    }

    private static void WriteNode(Stream s, JsonNode? node)
    {
        switch (node)
        {
            case null:
                s.WriteByte(0xf6); // simple value 22 = null
                return;
            case JsonObject obj:
                WriteObject(s, obj);
                return;
            case JsonArray arr:
                WriteArray(s, arr);
                return;
            case JsonValue v:
                WriteValue(s, v);
                return;
            default:
                throw new NotSupportedException($"unsupported JsonNode type: {node.GetType().Name}");
        }
    }

    private static void WriteObject(Stream s, JsonObject obj)
    {
        // Collect + sort keys by UTF-8 byte sequence.
        var entries = new List<KeyValuePair<byte[], JsonNode?>>(obj.Count);
        foreach (var kvp in obj)
            entries.Add(new KeyValuePair<byte[], JsonNode?>(Encoding.UTF8.GetBytes(kvp.Key), kvp.Value));
        entries.Sort((a, b) => CompareBytes(a.Key, b.Key));

        WriteTypeAndLength(s, 5, (ulong)entries.Count);
        foreach (var e in entries)
        {
            WriteTypeAndLength(s, 3, (ulong)e.Key.Length);
            s.Write(e.Key, 0, e.Key.Length);
            WriteNode(s, e.Value);
        }
    }

    private static void WriteArray(Stream s, JsonArray arr)
    {
        WriteTypeAndLength(s, 4, (ulong)arr.Count);
        foreach (var item in arr) WriteNode(s, item);
    }

    private static void WriteValue(Stream s, JsonValue v)
    {
        // JsonValue may wrap a JsonElement or a directly-assigned CLR value.
        // GetValueKind works for both; typed TryGetValue<T> handles extraction.
        var kind = v.GetValueKind();
        switch (kind)
        {
            case JsonValueKind.String:
                var utf8 = Encoding.UTF8.GetBytes(v.GetValue<string>());
                WriteTypeAndLength(s, 3, (ulong)utf8.Length);
                s.Write(utf8, 0, utf8.Length);
                return;
            case JsonValueKind.True:
                s.WriteByte(0xf5);
                return;
            case JsonValueKind.False:
                s.WriteByte(0xf4);
                return;
            case JsonValueKind.Null:
                s.WriteByte(0xf6);
                return;
            case JsonValueKind.Number:
                WriteNumber(s, v);
                return;
            default:
                throw new NotSupportedException($"unsupported JSON value kind: {kind}");
        }
    }

    private static void WriteNumber(Stream s, JsonValue v)
    {
        // JsonValue<T>.TryGetValue<U> is strict about T == U (an int-backed
        // value won't satisfy TryGetValue<long>). Round-trip through JSON text
        // to get a uniform representation that parses as an integer when
        // possible, otherwise as a 64-bit float.
        var text = v.ToJsonString();
        if (long.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var i64))
        {
            if (i64 >= 0) WriteTypeAndLength(s, 0, (ulong)i64);
            else          WriteTypeAndLength(s, 1, (ulong)(-(i64 + 1)));
            return;
        }
        if (ulong.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var u64))
        {
            WriteTypeAndLength(s, 0, u64);
            return;
        }
        if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
            throw new NotSupportedException($"cannot encode JSON number: {text}");
        s.WriteByte(0xfb);
        Span<byte> be = stackalloc byte[8];
        var bits = BitConverter.DoubleToInt64Bits(d);
        for (var i = 0; i < 8; i++) be[i] = (byte)(bits >> (56 - 8 * i));
        s.Write(be);
    }

    private static void WriteTypeAndLength(Stream s, byte major, ulong length)
    {
        var prefix = (byte)(major << 5);
        if (length < 24)                 s.WriteByte((byte)(prefix | (byte)length));
        else if (length <= byte.MaxValue)   { s.WriteByte((byte)(prefix | 24)); s.WriteByte((byte)length); }
        else if (length <= ushort.MaxValue) { s.WriteByte((byte)(prefix | 25)); WriteBe(s, length, 2); }
        else if (length <= uint.MaxValue)   { s.WriteByte((byte)(prefix | 26)); WriteBe(s, length, 4); }
        else                                { s.WriteByte((byte)(prefix | 27)); WriteBe(s, length, 8); }
    }

    private static void WriteBe(Stream s, ulong value, int bytes)
    {
        Span<byte> buf = stackalloc byte[bytes];
        for (var i = 0; i < bytes; i++) buf[i] = (byte)(value >> (8 * (bytes - 1 - i)));
        s.Write(buf);
    }

    // DAG-CBOR canonical map-key ordering: sort by the key's *CBOR-encoded*
    // bytes, which for text keys means shortest length first, then bytewise
    // lexicographic within the same length. This matches the atproto-dasl
    // reference encoder (serialize the key, sort by the serialized bytes).
    private static int CompareBytes(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return a.Length - b.Length;
        for (var i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return a[i] < b[i] ? -1 : 1;
        return 0;
    }
}
