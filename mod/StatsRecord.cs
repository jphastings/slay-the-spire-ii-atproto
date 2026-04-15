using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AtprotoTracker;

internal sealed class StatsRecord
{
    [JsonPropertyName("$type")]      public string Type => "games.gamesgamesgamesgames.actor.stats";
    [JsonPropertyName("game")]       public JsonNode Game { get; set; } = null!; // gameRef object
    [JsonPropertyName("source")]     public string Source { get; set; } = "steam";
    [JsonPropertyName("playtime")]   public int PlaytimeMinutes { get; set; }
    [JsonPropertyName("lastPlayed")] public string LastPlayed { get; set; } = "";
    [JsonPropertyName("createdAt")]  public string CreatedAt { get; set; } = "";
}
