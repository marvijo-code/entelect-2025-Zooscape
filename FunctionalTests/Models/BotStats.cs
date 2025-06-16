namespace FunctionalTests.Models;

public class BotStats
{
    public string Nickname { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Wins { get; set; }
    public int SecondPlaces { get; set; }
    public int GamesPlayed { get; set; }
}
