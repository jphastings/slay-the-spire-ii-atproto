using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sts2At;

internal sealed class RunRecord
{
    [JsonPropertyName("$type")]      public string Type => "at.sts2.run";
    [JsonPropertyName("outcome")]    public string Outcome { get; set; } = "";
    [JsonPropertyName("character")]  public string Character { get; set; } = "";
    [JsonPropertyName("ascension")]  public int Ascension { get; set; }
    [JsonPropertyName("seed")]       public string Seed { get; set; } = "";
    [JsonPropertyName("finalFloor")] public int FinalFloor { get; set; }

    [JsonPropertyName("finalAct"),   JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FinalAct { get; set; }

    [JsonPropertyName("score"),      JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Score { get; set; }

    [JsonPropertyName("killedBy"),   JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? KilledBy { get; set; }

    [JsonPropertyName("startedAt")]       public string StartedAt { get; set; } = "";
    [JsonPropertyName("endedAt")]         public string EndedAt { get; set; } = "";
    [JsonPropertyName("durationSeconds")] public int DurationSeconds { get; set; }

    [JsonPropertyName("deck")]   public List<string> Deck { get; set; } = new();
    [JsonPropertyName("relics")] public List<string> Relics { get; set; } = new();

    [JsonPropertyName("game"),     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Game { get; set; }

    [JsonPropertyName("statsRef"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StatsRef { get; set; }

    [JsonPropertyName("modVersion")]  public string ModVersion { get; set; } = Plugin.ModVersion;
    [JsonPropertyName("gameVersion"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GameVersion { get; set; }

    [JsonPropertyName("createdAt")] public string CreatedAt { get; set; } = "";
}
