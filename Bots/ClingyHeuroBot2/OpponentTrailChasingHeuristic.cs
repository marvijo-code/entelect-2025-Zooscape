using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace HeuroBot.Bots.ClingyHeuroBot2.Heuristics;

public class OpponentTrailChasingHeuristic : IHeuristic
{
    public string Name => "OpponentTrailChasing";

    public decimal CalculateRawScore(GameState state, Animal me, BotAction move, ILogger? logger)
    {
        // TODO: Implement original HeuristicsImpl.OpponentTrailChasing logic from Heuristics.cs
        return 0m;
    }
}
