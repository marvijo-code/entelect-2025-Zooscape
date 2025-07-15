#pragma warning disable SKEXP0110
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using System.Linq;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class EmptyCellAvoidanceHeuristic : IHeuristic
{
    public string Name => "EmptyCellAvoidance";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var state = heuristicContext.CurrentGameState;
        var weights = heuristicContext.Weights;
        var (nx, ny) = heuristicContext.MyNewPosition;

        // Find cell content at new position
        var cell = state.Cells.FirstOrDefault(c => c.X == nx && c.Y == ny);
        if (cell == null)
            return 0m;

        // If it's empty and there are pellets elsewhere, apply penalty
        if (cell.Content == CellContent.Empty && state.Cells.Any(c => c.Content == CellContent.Pellet))
        {
            return -weights.EmptyCellAvoidance;
        }

        return 0m;
    }
}
