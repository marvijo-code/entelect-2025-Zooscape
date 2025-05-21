using FunctionalTests;
using Marvijo.Zooscape.Bots.Common.Enums;

namespace Marvijo.Zooscape.Bots.FunctionalTests;

public class FunctionalTestParams
{
    public required string TestName { get; set; }
    public required string TestFileName { get; set; } // JSON game state file
    public string? BotIdToTest { get; set; } // Specific BotId if testing one bot, null for all
    public required List<BotAction> AcceptableActions { get; set; }
    public BotAction? ExpectedAction { get; set; } // Optional: if there is one most preferred action
    public int? Tick { get; set; }
    public Position? StartingPosition { get; set; }
}
