using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class ShortestPathToGoalHeuristic : IHeuristic
{
    public string Name => "ShortestPathToGoal";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        heuristicContext.Logger?.Verbose("{Heuristic} not implemented", Name);
        // Access GameState, Animal, BotAction via context if needed for actual logic
        // var state = context.CurrentGameState;
        // var me = context.CurrentAnimal;
        // var move = context.CurrentMove;
        return 0m;
    }
}
