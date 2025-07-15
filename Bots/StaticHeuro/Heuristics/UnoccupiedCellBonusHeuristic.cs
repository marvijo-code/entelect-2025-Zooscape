using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace StaticHeuro.Heuristics;

public class UnoccupiedCellBonusHeuristic : IHeuristic
{
    public string Name => "UnoccupiedCellBonus";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var state = heuristicContext.CurrentGameState;
        var weights = heuristicContext.Weights;
        var (nx, ny) = heuristicContext.MyNewPosition;

        // If any animal (including me) already occupies the destination cell, no bonus.
        if (state.Animals.Any(a => a.X == nx && a.Y == ny))
        {
            return 0m;
        }

        // If a zookeeper occupies destination cell => penalise (avoid)
        if (state.Zookeepers.Any(z => z.X == nx && z.Y == ny))
        {
            return -(weights.UnoccupiedCellBonus); // negative of bonus acts as penalty
        }

        // Bonus scaled down if we've already visited this cell a lot.
        int visitCount = heuristicContext.GetVisitCount((nx, ny));
        decimal divisor = 1m + visitCount; // 1,2,3 etc
        return weights.UnoccupiedCellBonus / divisor;
    }
}
