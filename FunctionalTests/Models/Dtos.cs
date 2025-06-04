namespace FunctionalTests.Models;

/// <summary>
/// DTO for test definition data transfer
/// </summary>
public class TestDefinitionDto
{
    public required string TestName { get; set; }
    public required string GameStateFile { get; set; }
    public string? Description { get; set; }
    public string? BotNickname { get; set; }
    public string? ExpectedAction { get; set; }
    public List<string> AcceptableActions { get; set; } = [];
    public required string TestType { get; set; }
    public bool TickOverride { get; set; }
    public List<string> Bots { get; set; } = [];
}

/// <summary>
/// DTO for test execution results
/// </summary>
public class TestResultDto
{
    public required string TestName { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public long ExecutionTimeMs { get; set; }
    public List<BotResultDto> BotResults { get; set; } = [];
    public string? GameStateFile { get; set; }
    public string? TestType { get; set; }
}

/// <summary>
/// DTO for individual bot test results
/// </summary>
public class BotResultDto
{
    public required string BotType { get; set; }
    public string? Action { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? BotId { get; set; }
    public int? InitialScore { get; set; }
    public int? FinalScore { get; set; }
    public int? ScoreDelta { get; set; }
    public Dictionary<string, object> PerformanceMetrics { get; set; } = new();
}

/// <summary>
/// DTO for creating new test definitions
/// </summary>
public class CreateTestRequest
{
    public required string TestName { get; set; }
    public required string GameStateFile { get; set; }
    public string? Description { get; set; }
    public string? BotNickname { get; set; }
    public Marvijo.Zooscape.Bots.Common.Enums.BotAction? ExpectedAction { get; set; }
    public List<Marvijo.Zooscape.Bots.Common.Enums.BotAction>? AcceptableActions { get; set; }
    public required string TestType { get; set; }
    public bool TickOverride { get; set; }
    public List<string>? Bots { get; set; }
    public object? CurrentGameState { get; set; } // The actual game state JSON data from visualizer
}
