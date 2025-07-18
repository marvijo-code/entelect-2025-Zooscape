#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System;
using System.Linq;
// using ClingyHeuroBot2; // Removed as Heuristics.ApplyMove is no longer used from local
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace StaticHeuro.Heuristics;

public class EdgeSafetyHeuristic : IHeuristic
{
    public string Name => "EdgeSafety";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition; // Updated

        if (!heuristicContext.CurrentGameState.Cells.Any())
            return 0m; // Should not happen in a valid game state

        // Cache board bounds to avoid repeated LINQ operations
        var cells = heuristicContext.CurrentGameState.Cells;
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (var cell in cells)
        {
            if (cell.X < minX) minX = cell.X;
            if (cell.X > maxX) maxX = cell.X;
            if (cell.Y < minY) minY = cell.Y;
            if (cell.Y > maxY) maxY = cell.Y;
        }

        // Calculate distance to the closest edge
        int distToClosestEdge = Math.Min(
            Math.Min(nx - minX, maxX - nx), // Distance to left/right edge
            Math.Min(ny - minY, maxY - ny) // Distance to top/bottom edge
        );

        if (distToClosestEdge == 0)
            return -heuristicContext.Weights.EdgeSafetyPenalty_0; // Directly on an edge
        else if (distToClosestEdge == 1)
            return -heuristicContext.Weights.EdgeSafetyPenalty_1; // One step away from an edge
        else if (distToClosestEdge == 2)
            return -heuristicContext.Weights.EdgeSafetyPenalty_2; // Two steps away from an edge

        return 0m; // Safe distance from edges
    }
}
