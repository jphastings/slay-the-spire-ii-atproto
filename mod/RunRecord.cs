using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AtprotoTracker;

internal sealed class RunRecord
{
    [JsonPropertyName("$type")]      public string Type => "me.byjp.pesos.sts2.run";
    [JsonPropertyName("outcome")]    public string Outcome { get; set; } = "";
    [JsonPropertyName("character")]  public string Character { get; set; } = "";
    [JsonPropertyName("ascension")]  public int Ascension { get; set; }
    [JsonPropertyName("seed")]       public string Seed { get; set; } = "";
    [JsonPropertyName("floor")] public int Floor { get; set; }

    [JsonPropertyName("act"),   JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Act { get; set; }

    [JsonPropertyName("score"),      JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Score { get; set; }

    [JsonPropertyName("killedBy"),   JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? KilledBy { get; set; }

    [JsonPropertyName("startedAt"),       JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StartedAt { get; set; }

    [JsonPropertyName("endedAt"),         JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EndedAt { get; set; }

    [JsonPropertyName("durationSeconds"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int DurationSeconds { get; set; }

    [JsonPropertyName("deck")]    public List<string> Deck { get; set; } = new();
    [JsonPropertyName("relics")]  public List<string> Relics { get; set; } = new();
    [JsonPropertyName("potions")] public List<string> Potions { get; set; } = new();

    [JsonPropertyName("allies"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AllyEntry>? Allies { get; set; }

    [JsonPropertyName("game"),     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Game { get; set; }

    [JsonPropertyName("statsRef"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StatsRef { get; set; }

    [JsonPropertyName("modVersion")]  public string ModVersion { get; set; } = Plugin.ModVersion;
    [JsonPropertyName("gameVersion"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GameVersion { get; set; }

    [JsonPropertyName("updatedAt")] public string UpdatedAt { get; set; } = "";
}

internal sealed class AllyEntry
{
    private const string Nsid = "me.byjp.pesos.sts2.run";

    [JsonPropertyName("$type")] public string Type { get; set; } = "";

    [JsonPropertyName("steamID64"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SteamID64 { get; set; }

    [JsonPropertyName("did"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Did { get; set; }

    public static AllyEntry FromSteam(ulong steamId64) =>
        new() { Type = $"{Nsid}#steamid", SteamID64 = steamId64.ToString() };

    public static AllyEntry FromDid(string did) =>
        new() { Type = $"{Nsid}#did", Did = did };
}
