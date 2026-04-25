using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace AtprotoTracker.Signing;

/// <summary>
/// Build-time private signing key. Embedded into the assembly as a resource
/// by a Release-only MSBuild target; absent in local development builds.
/// </summary>
internal static class ModSigningKey
{
    public const string ResourceName = "AtprotoTracker.signing-private-key.txt";
    public const string AttestationType = "me.byjp.pesos.sts2.run#attestation";

    private static readonly Lazy<Loaded?> _loaded = new(LoadOnce);

    public static bool Available => _loaded.Value is not null;
    public static DidKey? PrivateKey => _loaded.Value?.PrivateKey;
    public static string? PublicDidKey => _loaded.Value?.PublicDidKey;

    private sealed record Loaded(DidKey PrivateKey, string PublicDidKey);

    private static Loaded? LoadOnce()
    {
        var asm = typeof(ModSigningKey).Assembly;
        using var stream = asm.GetManifestResourceStream(ResourceName);
        if (stream is null) return null;

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var didKeyText = reader.ReadToEnd().Trim();
        if (string.IsNullOrEmpty(didKeyText)) return null;

        try
        {
            var priv = DidKey.Parse(didKeyText);
            var pub  = priv.DerivePublicDidKey();
            return new Loaded(priv, pub);
        }
        catch (Exception ex)
        {
            Log.Warn($"failed to load embedded signing key: {ex.Message}");
            return null;
        }
    }
}
