using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AtprotoTracker;

internal sealed class RunRecord
{
    [JsonPropertyName("$type")]      public string Type => "me.byjp.pesos.sts2.run";
    [JsonPropertyName("outcome")]    public string Outcome { get; set; } = "";
    [JsonPropertyName("character")]  public string Character { get; set; } = "";
    [JsonPropertyName("ascension")]  public int Ascension { get; set; }
    [JsonPropertyName("seed")]       public string Seed { get; set; } = "";

    [JsonPropertyName("steamID64"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SteamID64 { get; set; }
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

    [JsonPropertyName("stats"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CombatStatsSnapshot? Stats { get; set; }

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
    [JsonPropertyName("steam")] public string Steam { get; set; } = "";

    [JsonPropertyName("atproto"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Atproto { get; set; }
}

internal sealed class CombatStatsSnapshot
{
    [JsonPropertyName("combats"),                JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int Combats                { get; set; }
    [JsonPropertyName("combatsWon"),             JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int CombatsWon             { get; set; }
    [JsonPropertyName("elitesWon"),              JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int ElitesWon              { get; set; }
    [JsonPropertyName("bossesWon"),              JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int BossesWon              { get; set; }
    [JsonPropertyName("turns"),                  JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int Turns                  { get; set; }
    [JsonPropertyName("longestCombat"),          JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int LongestCombat          { get; set; }
    [JsonPropertyName("damageDealt"),            JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int DamageDealt            { get; set; }
    [JsonPropertyName("damageTaken"),            JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int DamageTaken            { get; set; }
    [JsonPropertyName("biggestDamageDealt"),     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int BiggestDamageDealt     { get; set; }
    [JsonPropertyName("biggestDamageTaken"),     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int BiggestDamageTaken     { get; set; }
    [JsonPropertyName("biggestTurnDamageDealt"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int BiggestTurnDamageDealt { get; set; }
    [JsonPropertyName("biggestTurnDamageTaken"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int BiggestTurnDamageTaken { get; set; }
    [JsonPropertyName("cardsPlayed"),            JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int CardsPlayed            { get; set; }
    [JsonPropertyName("cardsDrawn"),             JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int CardsDrawn             { get; set; }
    [JsonPropertyName("cardsExhausted"),         JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int CardsExhausted         { get; set; }
    [JsonPropertyName("potionsUsed"),            JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int PotionsUsed            { get; set; }
    [JsonPropertyName("noDamageTurns"),          JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int NoDamageTurns          { get; set; }
    [JsonPropertyName("highestBlockInTurn"),     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int HighestBlockInTurn     { get; set; }

    [JsonPropertyName("hitsDealtDistribution"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     JsonConverter(typeof(IntKeyedDictionaryConverter))]
    public Dictionary<int, int>? HitsDealtDistribution { get; set; }

    [JsonPropertyName("hitsTakenDistribution"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     JsonConverter(typeof(IntKeyedDictionaryConverter))]
    public Dictionary<int, int>? HitsTakenDistribution { get; set; }

    [JsonPropertyName("cardUseDistribution"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     JsonConverter(typeof(StringKeyedDictionaryConverter))]
    public Dictionary<string, int>? CardUseDistribution { get; set; }
}

/// <summary>
/// Serializes Dictionary&lt;int,int&gt; with string keys sorted numerically ascending
/// so the emitted JSON is deterministic and reads naturally.
/// </summary>
internal sealed class IntKeyedDictionaryConverter : JsonConverter<Dictionary<int, int>>
{
    public override Dictionary<int, int> Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        => throw new System.NotSupportedException();

    public override void Write(Utf8JsonWriter writer, Dictionary<int, int> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        var keys = new List<int>(value.Keys);
        keys.Sort();
        foreach (var k in keys) writer.WriteNumber(k.ToString(System.Globalization.CultureInfo.InvariantCulture), value[k]);
        writer.WriteEndObject();
    }
}

/// <summary>
/// Serializes Dictionary&lt;string,int&gt; with keys sorted alphabetically so
/// repeated runs produce byte-identical JSON.
/// </summary>
internal sealed class StringKeyedDictionaryConverter : JsonConverter<Dictionary<string, int>>
{
    public override Dictionary<string, int> Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        => throw new System.NotSupportedException();

    public override void Write(Utf8JsonWriter writer, Dictionary<string, int> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        var keys = new List<string>(value.Keys);
        keys.Sort(System.StringComparer.Ordinal);
        foreach (var k in keys) writer.WriteNumber(k, value[k]);
        writer.WriteEndObject();
    }
}
