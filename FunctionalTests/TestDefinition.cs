namespace FunctionalTests;

public class TestDefinition
{
    public required string TestName { get; set; }
    public required string GameStateFile { get; set; } // e.g., "my_test_scenario.json"
    public required List<string> AcceptableActions { get; set; } // Store as strings, convert to BotAction enum later
    public string? SpecificBotId { get; set; } // Optional: if this test is only for a specific bot (by string ID)
}
