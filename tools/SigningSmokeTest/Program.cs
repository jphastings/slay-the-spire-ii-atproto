using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using AtprotoTracker.Signing;

// Round-trips our inline signer through the Rust `atproto-attestation-verify`
// CLI. Exits 0 if all assertions pass.
//
// Usage: dotnet run --project tools/SigningSmokeTest [-- <path-to-rust-verify-cli>]
// Defaults to /Users/jp/src/ext/atproto-crates/target/debug/atproto-attestation-verify.
//
// Also supports deriving the public did:key from a P-256 private did:key, for
// verifying that a candidate MOD_SIGNING_PRIVATE_KEY matches the public key
// published at web/static/.well-known/sts2-mod-keys/keys.json:
//   dotnet run --project tools/SigningSmokeTest -- --derive-public did:key:z42t...

if (args.Length >= 2 && args[0] == "--derive-public")
{
    Console.WriteLine(DidKey.Parse(args[1]).DerivePublicDidKey());
    return 0;
}

var verifyCli = args.Length > 0
    ? args[0]
    : "/Users/jp/src/ext/atproto-crates/target/debug/atproto-attestation-verify";

// Throwaway test keypair generated once with atproto-identity-key generate p256.
// Not the production key — safe to commit.
const string TestPrivate = "did:key:z42tiSobABSFun5BWkfcpS3M5b2wR5cu9s2H8HuHC1CTN5fR";
const string TestPublic  = "did:key:zDnaezd72k6N6cNJZgYaKNo6zjUuySDZY5aJsr34xZhjf1veB";
const string TestRepoDid = "did:plc:test123";

var record = new JsonObject
{
    ["$type"] = "me.byjp.pesos.sts2.run",
    ["outcome"] = "victory",
    ["character"] = "ironclad",
    ["ascension"] = 0,
    ["seed"] = "TESTSEED",
    ["floor"] = 57,
    ["score"] = 1234,
    ["durationSeconds"] = 2718,
    ["deck"] = new JsonArray("strike", "defend", "bash"),
    ["relics"] = new JsonArray("burning_blood"),
    ["potions"] = new JsonArray(),
    ["modVersion"] = "0.0.0-test",
    ["updatedAt"] = "2026-04-23T12:34:56Z",
    // Simulate a pre-existing unrelated signature to confirm it's stripped before CID compute.
    ["signatures"] = new JsonArray(),
};

var metadata = new JsonObject
{
    ["$type"] = "me.byjp.pesos.sts2.modAttestation",
};

var privateKey = DidKey.Parse(TestPrivate);

// Verify public-key derivation — the same path ModSigningKey takes at runtime
// to avoid shipping both halves of the keypair.
var derivedPublic = privateKey.DerivePublicDidKey();
if (derivedPublic != TestPublic)
{
    Console.Error.WriteLine($"public-key derivation mismatch:\n  expected: {TestPublic}\n  derived:  {derivedPublic}");
    return 1;
}
Console.WriteLine("✓ DerivePublicDidKey matches expected public");

var attestation = InlineAttestation.CreateInline(record, metadata, TestRepoDid, privateKey, TestPublic);
InlineAttestation.Append(record, attestation);

var outDir = Path.Combine(Path.GetTempPath(), "sts2-sig-smoke");
Directory.CreateDirectory(outDir);
var signedPath = Path.Combine(outDir, "signed.json");
File.WriteAllText(signedPath, record.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"wrote {signedPath}");

AssertVerify(verifyCli, signedPath, TestRepoDid, expectSuccess: true, label: "signed (expect OK)");

// Tamper: mutate a primitive field, expect verify to fail.
var tampered = (JsonObject)JsonNode.Parse(File.ReadAllText(signedPath))!;
tampered["score"] = 9999;
var tamperedPath = Path.Combine(outDir, "tampered.json");
File.WriteAllText(tamperedPath, tampered.ToJsonString());
AssertVerify(verifyCli, tamperedPath, TestRepoDid, expectSuccess: false, label: "tampered field (expect FAIL)");

// Replay: same signed record but wrong repo DID — must fail (repository binding).
AssertVerify(verifyCli, signedPath, "did:plc:other456", expectSuccess: false, label: "wrong repo DID (expect FAIL)");

Console.WriteLine("all assertions passed.");
return 0;

static void AssertVerify(string cli, string recordPath, string repoDid, bool expectSuccess, string label)
{
    var psi = new ProcessStartInfo(cli, $"\"{recordPath}\" {repoDid}")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    using var p = Process.Start(psi) ?? throw new InvalidOperationException($"failed to start {cli}");
    p.WaitForExit();
    var ok = p.ExitCode == 0;
    var marker = ok == expectSuccess ? "✓" : "✗";
    Console.WriteLine($"{marker} {label}: exit={p.ExitCode}");
    if (ok != expectSuccess)
    {
        Console.Error.WriteLine(p.StandardError.ReadToEnd());
        Environment.Exit(1);
    }
}
