using System.Text.Json.Serialization;

namespace FunctionalTests.Models;

public class BotStats
{
    public string Nickname { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Wins { get; set; }
    public int SecondPlaces { get; set; }
    public int GamesPlayed { get; set; }

    // Total number of times this bot was captured across all games (not serialized)
    [JsonIgnore]
    public int TotalCaptures { get; set; }

    // Average captures per game – exposed to frontend
    [JsonPropertyName("averageCaptures")]
    public double AverageCaptures { get; set; }
}
