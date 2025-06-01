namespace Marvijo.Zooscape.Bots.FunctionalTests;

public class BotEntry
{
    public required string Name { get; set; } // Bot's class name, e.g., "ClingyHeuroBot"
    public string? Id { get; set; } // Optional: a specific ID from the JSON, if needed for lookup beyond name
}

public class TestDefinition
{
    public required string Name { get; set; } // The "name" field from JSON, used as TestName
    public string? Description { get; set; }
    public required string StateFile { get; set; } // The "stateFile" field from JSON
    public required List<string> CorrectActions { get; set; } // AcceptableActions
    public List<BotEntry>? Bots { get; set; } // List of bots this definition applies to. If null/empty, might imply all bots or needs specific handling.
}
