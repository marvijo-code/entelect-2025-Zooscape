using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace HeuroBot.Bots.ClingyHeuroBot2.Heuristics;

public class SpawnTimeMinimizerHeuristic : IHeuristic
{
    public string Name => "SpawnTimeMinimizer";

    public decimal CalculateRawScore(GameState state, Animal me, BotAction move, ILogger? logger)
    {
        // TODO: Implement original HeuristicsImpl.SpawnTimeMinimizer logic from Heuristics.cs
        return 0m;
    }
}
