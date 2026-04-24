using System;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace AtprotoTracker.Signing;

internal static class InlineAttestation
{
    /// <summary>
    /// Computes the content CID for (record, metadata, repositoryDid), signs
    /// the 36-byte binary CID with the supplied P-256 private key (SHA-256
    /// ECDSA, IEEE P1363 r‖s, low-S normalized), and returns the attestation
    /// object to append to the record's <c>signatures</c> array.
    /// </summary>
    public static JsonObject CreateInline(
        JsonObject record,
        JsonObject metadata,
        string repositoryDid,
        DidKey privateKey,
        string publicDidKey)
    {
        if (privateKey.Type != DidKeyType.P256Private)
            throw new ArgumentException("only P-256 private keys are supported", nameof(privateKey));

        // `key` participates in the content CID — add it to the metadata before
        // computing the CID so our signed payload matches the reference
        // implementation (see atproto-attestation::create_inline_attestation).
        var metadataForCid = (JsonObject)metadata.DeepClone();
        metadataForCid["key"] = publicDidKey;

        var cidBin = ContentCid.ComputeBinary(record, metadataForCid, repositoryDid);
        var cidStr = "b" + Base32Lower.Encode(cidBin);

        using var ecdsa = LoadP256(privateKey.RawBytes);
        var sig = ecdsa.SignData(cidBin, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        if (sig.Length != 64) throw new CryptographicException($"expected 64-byte P-256 signature, got {sig.Length}");

        NormalizeLowS(sig);

        var attestation = metadataForCid;
        attestation["cid"] = cidStr;
        attestation["signature"] = new JsonObject { ["$bytes"] = Convert.ToBase64String(sig) };
        return attestation;
    }

    /// <summary>
    /// Attaches the attestation to <c>record.signatures</c>, creating the array if needed.
    /// </summary>
    public static void Append(JsonObject record, JsonObject attestation)
    {
        if (record["signatures"] is not JsonArray arr)
        {
            arr = new JsonArray();
            record["signatures"] = arr;
        }
        arr.Add(attestation);
    }

    private static ECDsa LoadP256(byte[] dBytes)
    {
        var ec = ECDsa.Create();
        ec.ImportECPrivateKey(DidKey.BuildP256Sec1Der(dBytes), out _);
        return ec;
    }

    // P-256 curve order n, and n/2 — used for low-S normalization.
    // BigInteger.Parse treats a leading non-zero high nibble as a sign bit, so
    // prepend "00" to force unsigned interpretation.
    private static readonly BigInteger N = BigInteger.Parse(
        "00FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551",
        NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    private static readonly BigInteger HalfN = N >> 1;

    private static void NormalizeLowS(byte[] sig)
    {
        // sig = r (32) ‖ s (32). Read s as unsigned big-endian, compare to n/2.
        var s = new BigInteger(sig.AsSpan(32, 32), isUnsigned: true, isBigEndian: true);
        if (s <= HalfN) return;
        var low = N - s;
        var lowBytes = low.ToByteArray(isUnsigned: true, isBigEndian: true);
        Array.Clear(sig, 32, 32);
        // Right-align into the 32-byte s slot.
        Buffer.BlockCopy(lowBytes, 0, sig, 32 + (32 - lowBytes.Length), lowBytes.Length);
    }
}
