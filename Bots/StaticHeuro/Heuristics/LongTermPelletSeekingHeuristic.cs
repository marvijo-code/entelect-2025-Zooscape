using System;
using System.Collections.Generic;
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace StaticHeuro.Heuristics
{
    public class LongTermPelletSeekingHeuristic : IHeuristic
    {
        private int _cachedTick = -1;
        private readonly List<PelletCluster> _pelletClusters = new List<PelletCluster>();
        private int _boardWidth;
        private int _boardHeight;

        private class PelletCluster
        {
            public int Size { get; set; }
            public (double x, double y) Center { get; set; }
        }

        public string Name => "LongTermPelletSeeking";

        public decimal CalculateScore(IHeuristicContext context)
        {
            if (context.CurrentMove == BotAction.UseItem)
            {
                return 0;
            }

            // Update clusters only once per tick
            if (_cachedTick != context.CurrentGameState.Tick)
            {
                FindAndCachePelletClusters(context.CurrentGameState);
                _cachedTick = context.CurrentGameState.Tick;
            }

            if (!_pelletClusters.Any())
            {
                return 0;
            }

            return CalculateGravityScore(context.MyNewPosition, context);
        }

        private void FindAndCachePelletClusters(GameState gameState)
        {
            _pelletClusters.Clear();
            _boardWidth = gameState.Cells.Max(c => c.X) + 1;
            _boardHeight = gameState.Cells.Max(c => c.Y) + 1;

            var pelletCells = new HashSet<(int x, int y)>(
                gameState.Cells.Where(c => c.Content == CellContent.Pellet).Select(c => (c.X, c.Y))
            );

            var visited = new HashSet<(int x, int y)>();

            foreach (var cell in pelletCells)
            {
                if (visited.Contains(cell))
                {
                    continue;
                }

                var currentCluster = new List<(int x, int y)>();
                var queue = new Queue<(int x, int y)>();

                queue.Enqueue(cell);
                visited.Add(cell);

                while (queue.Count > 0)
                {
                    var (x, y) = queue.Dequeue();
                    currentCluster.Add((x, y));

                    foreach (var dir in new[] { (0, 1), (1, 0), (0, -1), (-1, 0) })
                    {
                        var neighbor = (x + dir.Item1, y + dir.Item2);
                        if (pelletCells.Contains(neighbor) && !visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                if (currentCluster.Any())
                {
                    _pelletClusters.Add(new PelletCluster
                    {
                        Size = currentCluster.Count,
                        Center = (currentCluster.Average(c => c.x), currentCluster.Average(c => c.y))
                    });
                }
            }
        }

        private decimal CalculateGravityScore((int x, int y) position, IHeuristicContext heuristicContext)
        {
            decimal gravityScore = 0;

            foreach (var cluster in _pelletClusters)
            {
                double dx = cluster.Center.x - position.x;
                double dy = cluster.Center.y - position.y;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance > 0)
                {
                    // The score is proportional to the cluster size and inversely proportional to the distance.
                    // This prevents smaller, closer clusters from dominating the score over larger, more distant ones.
                    gravityScore += (decimal)(cluster.Size / distance) * heuristicContext.Weights.LongTermPelletSeekingFactor;
                }
            }

            return gravityScore;
        }
    }
}
