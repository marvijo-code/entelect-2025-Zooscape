#pragma warning disable SKEXP0110
using System;
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class EdgeAwarenessHeuristic : IHeuristic
{
    public string Name => "EdgeAwareness";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var state = heuristicContext.CurrentGameState;
        var weights = heuristicContext.Weights;
        var (nx, ny) = heuristicContext.MyNewPosition;

        if (!state.Cells.Any())
            return 0m;

        int minX = state.Cells.Min(c => c.X);
        int maxX = state.Cells.Max(c => c.X);
        int minY = state.Cells.Min(c => c.Y);
        int maxY = state.Cells.Max(c => c.Y);

        int distToEdge = Math.Min(
            Math.Min(nx - minX, maxX - nx),
            Math.Min(ny - minY, maxY - ny)
        );

        // Using reciprocal: closer => larger penalty magnitude
        return -weights.EdgeAwareness / (distToEdge + 1m);
    }
}
