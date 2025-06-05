// #pragma warning disable SKEXP0110 // Disable SK Experimental warning
using System;
using System.Linq;
using HeuroBot.Services; // For Heuristics.HeuristicsImpl static methods
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace HeuroBot.Bots.ClingyHeuroBot2.Heuristics;

public class PelletEfficiencyHeuristic : IHeuristic
{
    public string Name => "PelletEfficiency";

    public decimal CalculateRawScore(GameState state, Animal me, BotAction move, ILogger? logger)
    {
        var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);

        int pelletsInRange = 0;
        foreach (var cell in state.Cells.Where(c => c.Content == CellContent.Pellet))
        {
            int dist = Heuristics.HeuristicsImpl.ManhattanDistance(cell.X, cell.Y, nx, ny);
            if (dist <= 3) // Pellets within 3 units (Manhattan distance)
            {
                pelletsInRange++;
            }
        }

        decimal riskFactor = 1.0m;
        if (state.Zookeepers.Any())
        {
            // Calculate distance to the closest zookeeper from the new position
            int minDistanceToZookeeper = state.Zookeepers.Min(z =>
                Heuristics.HeuristicsImpl.ManhattanDistance(z.X, z.Y, nx, ny)
            );

            // Risk factor increases as distance to zookeeper decreases.
            // Use Math.Max to ensure riskFactor is at least 1 (to avoid division by zero or overly large scores if zookeeper is on the same cell).
            // A smaller distance means higher risk. If distance is 0 or 1, risk is 1.
            // If distance is, say, 5, risk is 5. This means pellets are less valuable if a zookeeper is close.
            riskFactor = Math.Max(1.0m, (decimal)minDistanceToZookeeper);
        }

        if (riskFactor == 0) // Should not happen due to Math.Max(1.0m, ...)
        {
            return pelletsInRange > 0 ? decimal.MaxValue : 0m; // Avoid division by zero, highly prioritize if pellets exist
        }

        return (decimal)pelletsInRange / riskFactor;
    }
}
