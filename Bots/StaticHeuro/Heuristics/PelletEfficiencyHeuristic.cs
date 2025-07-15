// #pragma warning disable SKEXP0110 // Disable SK Experimental warning
using System;
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Utils;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace StaticHeuro.Heuristics;

public class PelletEfficiencyHeuristic : IHeuristic
{
    public string Name => "PelletEfficiency";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var state = heuristicContext.CurrentGameState;
        var weights = heuristicContext.Weights;
        var me = heuristicContext.CurrentAnimal;
        var (nx, ny) = heuristicContext.MyNewPosition;

        // Find all pellets on the board
        var pellets = state.Cells.Where(c => c.Content == CellContent.Pellet).ToList();
        if (!pellets.Any())
        {
            // No pellets, nothing to evaluate
            return 0m;
        }

        // Distance to closest pellet BEFORE the move
        int curMinDist = pellets.Min(p => BotUtils.ManhattanDistance(p.X, p.Y, me.X, me.Y));
        // Distance to closest pellet AFTER the move
        int newMinDist = pellets.Min(p => BotUtils.ManhattanDistance(p.X, p.Y, nx, ny));

        int diff = curMinDist - newMinDist; // positive if we are closer after the move

        if (diff == 0)
        {
            return 0m; // Neutral – no progress toward pellets
        }

        // Scale by weights. Moving closer yields positive, further yields negative
        return weights.PelletEfficiency * diff;
    }
}
