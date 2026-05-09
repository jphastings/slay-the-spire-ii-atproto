using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AtprotoTracker;

internal sealed class Config
{
    [JsonPropertyName("handle")]      public string Handle { get; set; } = "";
    [JsonPropertyName("appPassword")] public string AppPassword { get; set; } = "";
    // Cached after first run so subsequent runs update the same actor.stats record.
    [JsonPropertyName("statsRkey")]   public string StatsRkey { get; set; } = "";

    // Stamped after a successful login so an offline boot still knows which
    // DID to bucket queued runs under. Invalidated implicitly when the user
    // edits Handle to something different (we re-resolve when next online).
    [JsonPropertyName("cachedHandle")] public string CachedHandle { get; set; } = "";
    [JsonPropertyName("cachedDid")]    public string CachedDid    { get; set; } = "";
    [JsonPropertyName("cachedPds")]    public string CachedPds    { get; set; } = "";

    // Highest run rkey we've already inspected for ally-DID backfill. On boot
    // we walk records and skip anything ≤ this marker so the work is a
    // one-time cost per record, not every-boot.
    [JsonPropertyName("allyBackfillCheckpoint")] public string AllyBackfillCheckpoint { get; set; } = "";

    [JsonIgnore] public string Path { get; private set; } = "";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };

    public static Config LoadOrCreate()
    {
        var dir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var path = System.IO.Path.Combine(dir, "config.json");
        Config cfg;
        if (File.Exists(path))
        {
            cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText(path), JsonOpts) ?? new Config();
        }
        else
        {
            cfg = new Config();
            File.WriteAllText(path, JsonSerializer.Serialize(cfg, JsonOpts));
            LogFirstRunBanner(path);
        }
        cfg.Path = path;
        if (string.IsNullOrWhiteSpace(cfg.Handle) || string.IsNullOrWhiteSpace(cfg.AppPassword))
            LogFirstRunBanner(path);
        return cfg;
    }

    private static void LogFirstRunBanner(string path)
    {
        Log.Warn("========================================================================");
        Log.Warn("atproto-tracker: not yet configured — runs will NOT be posted to your PDS.");
        Log.Warn($"Edit this file, then restart the game:");
        Log.Warn($"  {path}");
        Log.Warn("Set 'handle' to your atproto handle (e.g. you.bsky.social) and");
        Log.Warn("'appPassword' to an app password from https://bsky.app/settings/app-passwords");
        Log.Warn("========================================================================");
    }

    public void Save() => File.WriteAllText(Path, JsonSerializer.Serialize(this, JsonOpts));
}
