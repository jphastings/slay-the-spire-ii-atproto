using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2At;

internal sealed class Config
{
    [JsonPropertyName("pdsUrl")]      public string PdsUrl { get; set; } = "https://bsky.social";
    [JsonPropertyName("handle")]      public string Handle { get; set; } = "";
    [JsonPropertyName("appPassword")] public string AppPassword { get; set; } = "";
    [JsonPropertyName("gameRef")]     public string GameRef { get; set; } = "at://did:web:gamesgamesgamesgames.games/games.gamesgamesgamesgames.game/3mglj4k2edl2l";

    // Cached after first run so subsequent runs update the same actor.stats record.
    [JsonPropertyName("statsRkey")]   public string StatsRkey { get; set; } = "";

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
            Log.Warn($"wrote default config to {path} — fill in handle + appPassword");
        }
        cfg.Path = path;
        return cfg;
    }

    public void Save() => File.WriteAllText(Path, JsonSerializer.Serialize(this, JsonOpts));
}
