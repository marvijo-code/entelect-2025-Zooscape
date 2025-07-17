#pragma warning disable SKEXP0110
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using System.Collections.Generic;
using System.Linq;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class AdaptivePathfindingHeuristic : IHeuristic
{
    public string Name => "AdaptivePathfinding";

    public decimal CalculateScore(IHeuristicContext context)
    {
        // Skip invalid game state or item usage
        if (context.CurrentMove == BotAction.UseItem || context.CurrentGameState.Cells == null)
        {
            return 0m;
        }

        // Defer heavy pathfinding until after tick 10 to match StaticHeuro optimisation
        if (context.CurrentGameState.Tick < 10)
        {
            return 0m;
        }

        var start = context.MyNewPosition;
        var gameState = context.CurrentGameState;
        const int maxDepth = 8;

        var visited = new HashSet<(int X,int Y)> { start };
        var queue = new Queue<(int X,int Y,int Dist,int Pellets)>();
        queue.Enqueue((start.X, start.Y, 0, 0));

        decimal bestEfficiency = 0m;

        while (queue.Count > 0)
        {
            var (x, y, dist, pellets) = queue.Dequeue();
            if (dist >= maxDepth) continue;

            foreach (var dir in _neighborDirs)
            {
                var nx = x + dir.dx;
                var ny = y + dir.dy;
                if (!BotUtils.IsTraversable(gameState, nx, ny)) continue;
                if (!visited.Add((nx, ny))) continue;

                var cell = gameState.Cells.First(c => c.X == nx && c.Y == ny);
                var newPellets = pellets + (cell.Content == CellContent.Pellet ? 1 : 0);
                var newDist = dist + 1;

                if (newPellets > 0)
                {
                    var efficiency = (decimal)newPellets / newDist;
                    if (efficiency > bestEfficiency)
                    {
                        bestEfficiency = efficiency;
                    }
                }

                queue.Enqueue((nx, ny, newDist, newPellets));
            }
        }

        return bestEfficiency * context.Weights.AdaptivePathfinding;
    }

    private static readonly (int dx,int dy)[] _neighborDirs =
    [
        (0, 1),  // Down
        (0, -1), // Up
        (1, 0),  // Right
        (-1, 0)  // Left
    ];
}
