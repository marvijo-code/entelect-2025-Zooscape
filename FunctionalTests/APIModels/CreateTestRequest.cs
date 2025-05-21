using System.ComponentModel.DataAnnotations;

namespace FunctionalTests.APIModels; // Or your preferred namespace for API models

public class CreateTestRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public required string TestName { get; set; }

    [Required]
    public required string GameStateJson { get; set; } // The full JSON content of the game state

    [Required]
    [MinLength(1)] // Must have at least one acceptable action
    public required List<string> AcceptableActions { get; set; } // List of BotAction enum names as strings

    public string? SpecificBotId { get; set; } // Optional: if this test is primarily for a specific bot
}
