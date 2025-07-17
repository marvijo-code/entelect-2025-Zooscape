using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace StaticHeuro.Heuristics;

public class PelletEfficiencyHeuristic : IHeuristic
{
    public string Name => "PelletEfficiency";

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

        // Calculate efficiency based on pellet density in the direction we're moving
        var pelletPositions = heuristicContext.CurrentGameState.Cells
            .Where(c => c.Content == CellContent.Pellet)
            .Select(c => (c.X, c.Y))
            .ToHashSet();

        if (pelletPositions.Count == 0)
        {
            return 0m;
        }

        // Calculate pellet density in a 3x3 area around the new position
        int pelletsInArea = 0;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                var checkPos = (heuristicContext.MyNewPosition.X + dx, heuristicContext.MyNewPosition.Y + dy);
                if (pelletPositions.Contains(checkPos))
                {
                    pelletsInArea++;
                }
            }
        }

        // Return efficiency score based on pellet density
        return pelletsInArea * heuristicContext.Weights.PelletEfficiency;
    }
}