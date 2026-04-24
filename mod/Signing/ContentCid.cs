using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace AtprotoTracker.Signing;

/// <summary>
/// Implements the CID-first attestation content CID per the badge.blue spec:
/// strip <c>signatures</c> from the record, build a transient <c>$sig</c>
/// from the attestation metadata (with <c>repository</c> injected, and
/// <c>cid</c>/<c>signature</c> removed), DAG-CBOR encode, SHA-256, and wrap
/// as a CIDv1 (codec 0x71, SHA-256 multihash).
/// </summary>
internal static class ContentCid
{
    /// <summary>
    /// Returns the 36-byte binary form <c>01 71 12 20 &lt;digest&gt;</c>.
    /// </summary>
    public static byte[] ComputeBinary(JsonNode record, JsonNode metadata, string repositoryDid)
    {
        if (record is not JsonObject recObj)
            throw new ArgumentException("record must be a JSON object", nameof(record));
        if (metadata is not JsonObject metaObj)
            throw new ArgumentException("metadata must be a JSON object", nameof(metadata));
        if (recObj["$type"] is not JsonValue || string.IsNullOrEmpty(recObj["$type"]!.GetValue<string>()))
            throw new ArgumentException("record must have a non-empty $type");
        if (metaObj["$type"] is not JsonValue || string.IsNullOrEmpty(metaObj["$type"]!.GetValue<string>()))
            throw new ArgumentException("metadata must have a non-empty $type");

        // Step 2: strip signatures from a deep clone of the record.
        var stripped = (JsonObject)recObj.DeepClone();
        stripped.Remove("signatures");

        // Step 3: prepare $sig metadata — clone, strip cid/signature, inject repository.
        var sig = (JsonObject)metaObj.DeepClone();
        sig.Remove("cid");
        sig.Remove("signature");
        sig["repository"] = repositoryDid;

        // Step 4: merge.
        stripped["$sig"] = sig;

        // Steps 5-7: DAG-CBOR → SHA-256 → CIDv1 wrap.
        var cbor = DagCbor.Encode(stripped);
        var digest = SHA256.HashData(cbor);

        var cid = new byte[36];
        cid[0] = 0x01; // CID v1
        cid[1] = 0x71; // dag-cbor codec
        cid[2] = 0x12; // multihash: sha2-256
        cid[3] = 0x20; // multihash length: 32
        Buffer.BlockCopy(digest, 0, cid, 4, 32);
        return cid;
    }

    /// <summary>
    /// Returns the string form of the content CID: <c>b</c> + base32-lower of
    /// the 36 binary bytes, prefix <c>bafyrei</c>.
    /// </summary>
    public static string ComputeString(JsonNode record, JsonNode metadata, string repositoryDid)
    {
        var bin = ComputeBinary(record, metadata, repositoryDid);
        return "b" + Base32Lower.Encode(bin);
    }
}

/// <summary>
/// Lowercase RFC 4648 base32, no padding (the multibase 'b' alphabet).
/// </summary>
internal static class Base32Lower
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz234567";

    public static string Encode(ReadOnlySpan<byte> input)
    {
        var sb = new StringBuilder((input.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in input)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                sb.Append(Alphabet[(buffer >> bits) & 0x1f]);
            }
        }
        if (bits > 0) sb.Append(Alphabet[(buffer << (5 - bits)) & 0x1f]);
        return sb.ToString();
    }
}
