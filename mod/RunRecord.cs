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
    [JsonPropertyName("biggestSingleHit"),       JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int BiggestSingleHit       { get; set; }
    [JsonPropertyName("biggestTurnDamage"),      JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int BiggestTurnDamage      { get; set; }
    [JsonPropertyName("biggestTurnDamageTaken"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int BiggestTurnDamageTaken { get; set; }
    [JsonPropertyName("cardsPlayed"),            JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int CardsPlayed            { get; set; }
    [JsonPropertyName("cardsDrawn"),             JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int CardsDrawn             { get; set; }
    [JsonPropertyName("cardsExhausted"),         JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int CardsExhausted         { get; set; }
    [JsonPropertyName("potionsUsed"),            JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int PotionsUsed            { get; set; }
    [JsonPropertyName("noDamageTurns"),          JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int NoDamageTurns          { get; set; }
    [JsonPropertyName("highestBlockInTurn"),     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int HighestBlockInTurn     { get; set; }
}
