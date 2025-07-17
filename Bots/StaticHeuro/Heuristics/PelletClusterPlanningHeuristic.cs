using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StaticHeuro.Heuristics;

/// <summary>
/// Optimized heuristic for long-range planning toward large pellet clusters
/// Designed to work within 200ms time constraints alongside other heuristics
/// </summary>
public class PelletClusterPlanningHeuristic : IHeuristic
{
    public string Name => "PelletClusterPlanning";
    private const int MIN_CLUSTER_SIZE = 3;
    private const int MAX_CLUSTER_DISTANCE = 2;
    private const int MAX_SEARCH_RANGE = 12; // Reduced from 15 for performance
    private const int CACHE_REFRESH_INTERVAL = 30; // Increased from 25 to reduce computation frequency
    private const int MAX_PELLETS_TO_PROCESS = 50; // Limit pellet processing for performance
    private static readonly Dictionary<string, ClusterAnalysis> _clusterCache = new();
    private static int _lastCacheUpdate = -1;
    
    private record ClusterAnalysis(
        List<PelletCluster> Clusters,
        Dictionary<(int X, int Y), int> ClusterMembership
    );
    
    private record PelletCluster(
        (int X, int Y) Center,
        List<(int X, int Y)> Pellets,
        decimal Value,
        decimal Density
    );

    public decimal CalculateScore(IHeuristicContext context)
    {
        if (context.CurrentMove == BotAction.UseItem || context.CurrentGameState.Cells == null)
        {
            return 0m;
        }

        var gameState = context.CurrentGameState;
        var myPos = context.MyNewPosition;
        var newPos = context.MyNewPosition;
        
        // Update cluster analysis cache periodically for performance
        var cacheKey = $"clusters_{gameState.Tick}";
        if (_lastCacheUpdate != gameState.Tick || !_clusterCache.ContainsKey(cacheKey))
        {
            if (gameState.Tick - _lastCacheUpdate >= CACHE_REFRESH_INTERVAL)
            {
                UpdateClusterCache(gameState, cacheKey);
                _lastCacheUpdate = gameState.Tick;
            }
        }

        if (!_clusterCache.TryGetValue(cacheKey, out var analysis))
        {
            return 0m;
        }

        // Score the move based on its alignment with optimal cluster targeting
        var clusterScore = CalculateClusterAlignmentScore(analysis, myPos, newPos, gameState);
        
        return clusterScore * context.Weights.PelletClusterPlanning;
    }

    private void UpdateClusterCache(GameState gameState, string cacheKey)
    {
        // Clear old cache entries to prevent memory bloat
        if (_clusterCache.Count > 5)
        {
            var oldestKey = _clusterCache.Keys.First();
            _clusterCache.Remove(oldestKey);
        }

        // Early exit if too many pellets to process efficiently
        var pellets = gameState.Cells
            .Where(c => c.Content == CellContent.Pellet)
            .Take(MAX_PELLETS_TO_PROCESS) // Limit for performance
            .Select(c => (c.X, c.Y))
            .ToList();
        
        // Early exit if no pellets or too few to form clusters
        if (pellets.Count < MIN_CLUSTER_SIZE)
        {
            _clusterCache[cacheKey] = new ClusterAnalysis(new List<PelletCluster>(), new Dictionary<(int, int), int>());
            return;
        }

        var clusters = FindPelletClusters(pellets, gameState);
        var membership = BuildClusterMembership(clusters);
        
        _clusterCache[cacheKey] = new ClusterAnalysis(clusters, membership);
    }

    private List<PelletCluster> FindPelletClusters(List<(int X, int Y)> pellets, GameState gameState)
    {
        var clusters = new List<PelletCluster>();
        var processed = new HashSet<(int X, int Y)>();
        
        foreach (var pellet in pellets)
        {
            if (processed.Contains(pellet)) continue;
            
            var cluster = BuildCluster(pellet, pellets, processed, gameState);
            if (cluster.Pellets.Count >= MIN_CLUSTER_SIZE)
            {
                clusters.Add(cluster);
            }
        }
        
        return clusters.OrderByDescending(c => c.Value).Take(5).ToList(); // Reduced from 8 to 5 for better performance
    }

    private PelletCluster BuildCluster((int X, int Y) seed, List<(int X, int Y)> allPellets, 
        HashSet<(int X, int Y)> processed, GameState gameState)
    {
        var clusterPellets = new List<(int X, int Y)>();
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue(seed);
        processed.Add(seed);
        
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            clusterPellets.Add(current);
            
            // Find nearby pellets within cluster distance - optimized with early exit
            var neighborsAdded = 0;
            foreach (var neighbor in allPellets)
            {
                if (processed.Contains(neighbor)) continue;
                
                if (ManhattanDistance(current, neighbor) <= MAX_CLUSTER_DISTANCE)
                {
                    queue.Enqueue(neighbor);
                    processed.Add(neighbor);
                    neighborsAdded++;
                    
                    // Limit cluster expansion to prevent performance issues
                    if (neighborsAdded >= 4) break;
                }
            }
        }
        
        var center = CalculateClusterCenter(clusterPellets);
        var value = CalculateClusterValue(clusterPellets, center, gameState);
        var density = (decimal)clusterPellets.Count / CalculateClusterArea(clusterPellets);
        
        return new PelletCluster(center, clusterPellets, value, density);
    }

    private (int X, int Y) CalculateClusterCenter(List<(int X, int Y)> pellets)
    {
        if (pellets.Count == 0) return (0, 0);
        
        var avgX = (int)Math.Round(pellets.Average(p => p.X));
        var avgY = (int)Math.Round(pellets.Average(p => p.Y));
        return (avgX, avgY);
    }

    private decimal CalculateClusterValue(List<(int X, int Y)> pellets, (int X, int Y) center, GameState gameState)
    {
        var baseValue = pellets.Count * 10m; // Base value per pellet
        
        // Bonus for cluster density
        var area = CalculateClusterArea(pellets);
        var densityBonus = area > 0 ? (pellets.Count * pellets.Count) / area : 0m;
        
        // Penalty for nearby zookeepers
        var zookeeperPenalty = 0m;
        var nearbyZookeepers = gameState.Zookeepers
            .Count(z => ManhattanDistance(center, (z.X, z.Y)) <= 6);
        
        if (nearbyZookeepers > 0)
        {
            zookeeperPenalty = nearbyZookeepers * 15m; // Significant penalty for danger
        }
        
        return Math.Max(0m, baseValue + densityBonus - zookeeperPenalty);
    }

    private decimal CalculateClusterArea(List<(int X, int Y)> pellets)
    {
        if (pellets.Count <= 1) return 1m;
        
        var minX = pellets.Min(p => p.X);
        var maxX = pellets.Max(p => p.X);
        var minY = pellets.Min(p => p.Y);
        var maxY = pellets.Max(p => p.Y);
        
        return Math.Max(1m, (maxX - minX + 1) * (maxY - minY + 1));
    }

    private Dictionary<(int X, int Y), int> BuildClusterMembership(List<PelletCluster> clusters)
    {
        var membership = new Dictionary<(int X, int Y), int>();
        
        for (int i = 0; i < clusters.Count; i++)
        {
            foreach (var pellet in clusters[i].Pellets)
            {
                membership[pellet] = i;
            }
        }
        
        return membership;
    }

    private decimal CalculateClusterAlignmentScore(ClusterAnalysis analysis, (int X, int Y) currentPos, 
        (int X, int Y) newPos, GameState gameState)
    {
        if (analysis.Clusters.Count == 0) return 0m;
        
        // Find the best cluster to target
        var bestCluster = analysis.Clusters
            .Where(c => ManhattanDistance(currentPos, c.Center) <= MAX_CLUSTER_DISTANCE)
            .OrderByDescending(c => c.Value / Math.Max(1, ManhattanDistance(currentPos, c.Center)))
            .FirstOrDefault();
        
        if (bestCluster == null) return 0m;
        
        // Score based on movement toward the best cluster
        var currentDistance = ManhattanDistance(currentPos, bestCluster.Center);
        var newDistance = ManhattanDistance(newPos, bestCluster.Center);
        
        var progressScore = 0m;
        if (newDistance < currentDistance)
        {
            progressScore = (currentDistance - newDistance) * 20m; // Reward progress toward cluster
        }
        else if (newDistance > currentDistance)
        {
            progressScore = (currentDistance - newDistance) * 5m; // Small penalty for moving away
        }
        
        // Bonus for moving toward high-value clusters
        var clusterValueBonus = bestCluster.Value * 0.1m;
        
        // Additional bonus if the new position is within the cluster
        var withinClusterBonus = 0m;
        if (bestCluster.Pellets.Any(p => ManhattanDistance(newPos, p) <= 2))
        {
            withinClusterBonus = 30m;
        }
        
        return progressScore + clusterValueBonus + withinClusterBonus;
    }

    private static int ManhattanDistance((int X, int Y) a, (int X, int Y) b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }
}
