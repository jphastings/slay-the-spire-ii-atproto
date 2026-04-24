using System;
using System.Security.Cryptography;

namespace AtprotoTracker.Signing;

internal enum DidKeyType
{
    P256Public,
    P256Private,
}

internal sealed record DidKey(DidKeyType Type, byte[] RawBytes)
{
    // Multicodec varint prefixes (see github.com/multiformats/multicodec table).
    // P-256: 0x1200 public, 0x1306 private. Encoded as 2-byte unsigned varints.
    private static readonly byte[] P256PublicPrefix  = { 0x80, 0x24 };
    private static readonly byte[] P256PrivatePrefix = { 0x86, 0x26 };

    public static DidKey Parse(string didKey)
    {
        const string scheme = "did:key:";
        if (!didKey.StartsWith(scheme, StringComparison.Ordinal))
            throw new FormatException("expected did:key: prefix");
        var body = didKey[scheme.Length..];
        if (body.Length == 0 || body[0] != 'z')
            throw new FormatException("only multibase base58btc ('z' prefix) supported");

        var decoded = Base58Btc.Decode(body[1..]);
        if (decoded.Length < 3)
            throw new FormatException("decoded did:key too short");

        if (decoded[0] == P256PrivatePrefix[0] && decoded[1] == P256PrivatePrefix[1])
        {
            if (decoded.Length != 2 + 32)
                throw new FormatException($"P-256 private key must be 32 bytes, got {decoded.Length - 2}");
            return new DidKey(DidKeyType.P256Private, decoded[2..]);
        }
        if (decoded[0] == P256PublicPrefix[0] && decoded[1] == P256PublicPrefix[1])
        {
            if (decoded.Length != 2 + 33)
                throw new FormatException($"P-256 compressed public key must be 33 bytes, got {decoded.Length - 2}");
            return new DidKey(DidKeyType.P256Public, decoded[2..]);
        }
        throw new FormatException($"unsupported multicodec prefix 0x{decoded[0]:X2}{decoded[1]:X2} (only P-256 supported)");
    }

    /// <summary>
    /// Derives the P-256 public <c>did:key</c> (compressed point) from this
    /// private key by letting the BCL compute the public point.
    /// </summary>
    public string DerivePublicDidKey()
    {
        if (Type != DidKeyType.P256Private)
            throw new InvalidOperationException("only P-256 private keys can derive a public did:key");

        using var ecdsa = ECDsa.Create();
        // Reuse the same SEC1 DER form InlineAttestation builds — keeps the
        // key-loading path identical between signing and derivation.
        var der = BuildP256Sec1Der(RawBytes);
        ecdsa.ImportECPrivateKey(der, out _);
        var p = ecdsa.ExportParameters(includePrivateParameters: false);

        // SEC1 compressed form: 0x02 | 0x03 prefix (parity of Y) then X.
        var x = p.Q.X ?? throw new InvalidOperationException("ExportParameters did not return Q.X");
        var y = p.Q.Y ?? throw new InvalidOperationException("ExportParameters did not return Q.Y");
        if (x.Length != 32 || y.Length != 32)
            throw new CryptographicException($"unexpected P-256 coord lengths X={x.Length} Y={y.Length}");

        var compressed = new byte[33];
        compressed[0] = (byte)(0x02 | (y[31] & 0x01));
        Buffer.BlockCopy(x, 0, compressed, 1, 32);

        var withPrefix = new byte[2 + 33];
        withPrefix[0] = 0x80; withPrefix[1] = 0x24;   // P-256 public multicodec
        Buffer.BlockCopy(compressed, 0, withPrefix, 2, 33);

        return "did:key:z" + Base58Btc.Encode(withPrefix);
    }

    internal static byte[] BuildP256Sec1Der(byte[] d)
    {
        if (d.Length != 32) throw new ArgumentException("P-256 private scalar must be 32 bytes");
        // SEC1 ECPrivateKey, named curve prime256v1 (OID 1.2.840.10045.3.1.7).
        var der = new byte[51]
        {
            0x30, 0x31, 0x02, 0x01, 0x01, 0x04, 0x20,
            d[ 0], d[ 1], d[ 2], d[ 3], d[ 4], d[ 5], d[ 6], d[ 7],
            d[ 8], d[ 9], d[10], d[11], d[12], d[13], d[14], d[15],
            d[16], d[17], d[18], d[19], d[20], d[21], d[22], d[23],
            d[24], d[25], d[26], d[27], d[28], d[29], d[30], d[31],
            0xA0, 0x0A, 0x06, 0x08, 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x03, 0x01, 0x07,
        };
        return der;
    }
}
