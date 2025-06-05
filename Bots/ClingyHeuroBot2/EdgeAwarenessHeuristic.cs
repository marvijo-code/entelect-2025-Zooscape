using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace HeuroBot.Bots.ClingyHeuroBot2.Heuristics;

public class EdgeAwarenessHeuristic : IHeuristic
{
    public string Name => "EdgeAwareness";

    public decimal CalculateRawScore(GameState state, Animal me, BotAction move, ILogger? logger)
    {
        // TODO: Implement original HeuristicsImpl.EdgeAwareness logic from Heuristics.cs
        return 0m;
    }
}
