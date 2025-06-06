#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class WallCollisionRiskHeuristic : IHeuristic
{
    public string Name => "WallCollisionRisk";

    public decimal CalculateRawScore(IHeuristicContext heuristicContext)
    {
        var (x, y) = Heuristics.ApplyMove(me.X, me.Y, move);
        int steps = 0;
        while (steps < 3 && Heuristics.IsTraversable(state, x, y))
        {
            (x, y) = Heuristics.ApplyMove(x, y, move);
            steps++;
        }
        if (!Heuristics.IsTraversable(state, x, y))
            steps--;

        return steps switch
        {
            0 => -2.0m, // would hit wall next tick
            1 => -0.8m,
            2 => -0.3m,
            _ => 0m,
        };
    }
}
