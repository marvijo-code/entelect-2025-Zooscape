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
    public string? BotNickname { get; set; }
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
