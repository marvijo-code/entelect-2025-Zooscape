using System.Text.Json.Serialization;
using Marvijo.Zooscape.Bots.Common.Enums;

namespace FunctionalTests.Models;

/// <summary>
/// Represents a test definition loaded from JSON
/// </summary>
public class TestDefinition
{
    public required string TestName { get; set; }
    public required string GameStateFile { get; set; }
    public string? Description { get; set; }
    /// <summary>
    /// The nickname of the bot to test, as it appears in the game state file.
    /// This is used to identify which bot's perspective to use for the test.
    /// This property was previously named 'BotNickname'.
    /// </summary>
    [JsonPropertyName("botNicknameInStateFile")]
    public string? BotNicknameInStateFile { get; set; }
    public BotAction? ExpectedAction { get; set; }
    public List<BotAction> AcceptableActions { get; set; } = [];
    public required TestType TestType { get; set; }
    public bool TickOverride { get; set; }
    public List<string> Bots { get; set; } = [];

    public override string ToString() => TestName ?? "Unnamed Test";
}

/// <summary>
/// Types of tests that can be performed
/// </summary>
public enum TestType
{
    SingleBot,
    MultiBotArray,
    GameStateLoad,
    TickOverride,
}
