using System;
using System.Collections.Generic;
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace ClingyHeuroBot2.Heuristics;

public class LongTermPelletSeekingHeuristic : IHeuristic
{
    public string Name => "LongTermPelletSeeking";

    public decimal CalculateScore(IHeuristicContext context)
    {
        // This heuristic should not apply when the action is to use an item.
        if (context.CurrentMove == BotAction.UseItem)
        {
            return 0;
        }

        var clusterScore = CalculatePelletClusterScore(context.CurrentGameState, context.MyNewPosition);
        return clusterScore;
    }

    private decimal CalculatePelletClusterScore(GameState gameState, (int x, int y) position)
    {
        // Use BFS to find the nearest large pellet cluster (>=10 pellets)
        var visited = new HashSet<(int x, int y)>();
        var queue = new Queue<(int x, int y, int distance)>();
        queue.Enqueue((position.x, position.y, 0));
        visited.Add((position.x, position.y));
        
        while (queue.Count > 0)
        {
            var (x, y, dist) = queue.Dequeue();
            
            // Check if current cell is part of a large cluster
            if (IsLargePelletCluster(gameState, x, y, 10))
                return 1.0m / (dist + 1);
                
            // Explore neighbors
            foreach (var dir in new[] { (0,1), (1,0), (0,-1), (-1,0) })
            {
                int nx = x + dir.Item1, ny = y + dir.Item2;
                if (IsValidCell(gameState, nx, ny) && !visited.Contains((nx, ny)))
                {
                    visited.Add((nx, ny));
                    queue.Enqueue((nx, ny, dist + 1));
                }
            }
        }
        
        return 0; // No large cluster found
    }

    private bool IsLargePelletCluster(GameState gameState, int startX, int startY, int threshold)
    {
        if (gameState.Cells.FirstOrDefault(c => c.X == startX && c.Y == startY)?.Content != CellContent.Pellet)
            return false;

        var visited = new HashSet<(int, int)>();
        var queue = new Queue<(int, int)>();
        queue.Enqueue((startX, startY));
        visited.Add((startX, startY));
        int clusterSize = 0;

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            clusterSize++;

            if (clusterSize >= threshold)
                return true;

            foreach (var dir in new[] { (0, 1), (1, 0), (0, -1), (-1, 0) })
            {
                int nx = x + dir.Item1, ny = y + dir.Item2;
                if (visited.Contains((nx, ny)))
                    continue;

                var neighbor = gameState.Cells.FirstOrDefault(c => c.X == nx && c.Y == ny);
                if (neighbor?.Content == CellContent.Pellet)
                {
                    visited.Add((nx, ny));
                    queue.Enqueue((nx, ny));
                }
            }
        }

        return false;
    }

    private bool IsValidCell(GameState gameState, int x, int y)
    {
        // Board dimensions are not direct properties, they must be inferred from the cell data.
        int boardWidth = gameState.Cells.Max(c => c.X) + 1;
        int boardHeight = gameState.Cells.Max(c => c.Y) + 1;

        return x >= 0 && x < boardWidth && y >= 0 && y < boardHeight &&
               gameState.Cells.FirstOrDefault(c => c.X == x && c.Y == y)?.Content != CellContent.Wall;
    }
}
