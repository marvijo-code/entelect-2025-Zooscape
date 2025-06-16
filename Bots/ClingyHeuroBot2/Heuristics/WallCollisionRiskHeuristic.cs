#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class WallCollisionRiskHeuristic : IHeuristic
{
    public string Name => "WallCollisionRisk";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var (x, y) = BotUtils.ApplyMove(
            heuristicContext.CurrentAnimal.X,
            heuristicContext.CurrentAnimal.Y,
            heuristicContext.CurrentMove
        );
        int steps = 0;
        while (steps < 3 && BotUtils.IsTraversable(heuristicContext.CurrentGameState, x, y))
        {
            (x, y) = BotUtils.ApplyMove(x, y, heuristicContext.CurrentMove);
            steps++;
        }
        if (!BotUtils.IsTraversable(heuristicContext.CurrentGameState, x, y))
            steps--;

        return steps switch
        {
            0 => heuristicContext.Weights.WallCollisionPenaltyImmediate, // would hit wall next tick
            1 => heuristicContext.Weights.WallCollisionPenaltyNear,
            2 => heuristicContext.Weights.WallCollisionPenaltyMidRange,
            _ => 0m,
        };
    }
}
