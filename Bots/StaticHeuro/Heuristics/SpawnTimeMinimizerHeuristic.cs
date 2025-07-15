using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace StaticHeuro.Heuristics;

public class SpawnTimeMinimizerHeuristic : IHeuristic
{
    public string Name => "SpawnTimeMinimizer";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        heuristicContext.Logger?.Verbose("{Heuristic} not implemented", Name);
        // Access GameState, Animal, BotAction via context if needed for actual logic
        // var state = heuristicContext.CurrentGameState;
        // var me = heuristicContext.CurrentAnimal;
        // var move = heuristicContext.CurrentMove;
        return 0m;
    }
}
