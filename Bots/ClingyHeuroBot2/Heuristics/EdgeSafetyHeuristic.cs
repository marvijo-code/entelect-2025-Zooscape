#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System;
using System.Linq;
using ClingyHeuroBot2;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class EdgeSafetyHeuristic : IHeuristic
{
    public string Name => "EdgeSafety";

    public decimal CalculateRawScore(GameState state, Animal me, BotAction move, ILogger? logger)
    {
        var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);

        if (!state.Cells.Any())
            return 0m; // Should not happen in a valid game state

        int minX = state.Cells.Min(c => c.X);
        int maxX = state.Cells.Max(c => c.X);
        int minY = state.Cells.Min(c => c.Y);
        int maxY = state.Cells.Max(c => c.Y);

        // Calculate distance to the closest edge
        int distToClosestEdge = Math.Min(
            Math.Min(nx - minX, maxX - nx), // Distance to left/right edge
            Math.Min(ny - minY, maxY - ny) // Distance to top/bottom edge
        );

        if (distToClosestEdge == 0)
            return -2.0m; // Directly on an edge
        else if (distToClosestEdge == 1)
            return -0.8m; // One step away from an edge
        else if (distToClosestEdge == 2)
            return -0.2m; // Two steps away from an edge

        return 0m; // Safe distance from edges
    }
}
