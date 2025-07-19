using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace StaticHeuro.Heuristics;

/// <summary>
/// Provides a large bonus for moves that land directly on a pellet.
/// This ensures immediate pellet collection is prioritized over other considerations.
/// </summary>
public class ImmediatePelletBonusHeuristic : IHeuristic
{
    public string Name => "ImmediatePelletBonus";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        // Check if the proposed move lands directly on a pellet
        var targetCell = heuristicContext.CurrentGameState.Cells
            .FirstOrDefault(c => c.X == heuristicContext.MyNewPosition.X && c.Y == heuristicContext.MyNewPosition.Y);

        if (targetCell?.Content == CellContent.Pellet)
        {
            // Return the immediate pellet bonus for landing directly on a pellet
            return heuristicContext.Weights.ImmediatePelletBonus;
        }

        return 0m;
    }
}
