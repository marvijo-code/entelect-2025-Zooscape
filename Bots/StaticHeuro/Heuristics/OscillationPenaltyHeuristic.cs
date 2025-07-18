using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using System;
using System.Reflection;

namespace StaticHeuro.Heuristics;

/// <summary>
/// Enhanced oscillation prevention heuristic that penalizes repetitive movement patterns.
/// This heuristic considers:
/// 1. Short-term oscillations (2-step patterns like Left↔Right or Up↔Down)
/// 2. Medium-term oscillations (returning to recently visited positions)
/// 3. Frequency-based penalties (positions visited multiple times get increasing penalties)
/// 4. Direction reversal penalties (changing direction repeatedly)
/// </summary>
public class OscillationPenaltyHeuristic : IHeuristic
{
    public string Name => "OscillationPenalty";
    
    // Configuration constants
    private const int MAX_HISTORY_CHECK = 5;
    private const decimal MAX_FREQUENCY_SCALING = 3.0m;
    private const decimal SHORT_OSCILLATION_FACTOR = 1.5m;
    private const decimal MEDIUM_OSCILLATION_FACTOR = 1.0m;
    private const decimal FREQUENCY_OSCILLATION_FACTOR = 0.5m;
    private const decimal REVERSAL_OSCILLATION_FACTOR = 0.75m;

    public decimal CalculateScore(IHeuristicContext ctx)
    {
        if (ctx.AnimalRecentPositions == null || ctx.AnimalRecentPositions.Count < 2)
            return 0m;

        decimal totalPenalty = 0m;
        var newPos = ctx.MyNewPosition;
        var positions = ctx.AnimalRecentPositions.ToArray();
        decimal penaltyWeight = GetWeightValue(ctx.Weights);
        
        // 1. Check for short-term oscillations (returning to position from 2 moves ago)
        if (ctx.AnimalRecentPositions.Count >= 3)
        {   
            var twoMovesAgo = positions[positions.Length - 3];
            if (newPos.X == twoMovesAgo.Item1 && newPos.Y == twoMovesAgo.Item2)
            {
                totalPenalty -= penaltyWeight * SHORT_OSCILLATION_FACTOR;
            }
        }
        
        // 2. Check for medium-term oscillations (returning to recently visited positions)
        int historyDepth = Math.Min(MAX_HISTORY_CHECK, positions.Length);
        for (int i = 1; i <= historyDepth; i++)
        {   
            var pastPos = positions[positions.Length - i];
            if (newPos.X == pastPos.Item1 && newPos.Y == pastPos.Item2)
            {   
                // Penalty scales with recency - more recent revisits are penalized more heavily
                decimal recencyFactor = 1.0m - ((decimal)i / historyDepth);
                totalPenalty -= penaltyWeight * MEDIUM_OSCILLATION_FACTOR * recencyFactor;
                break; // Only count the most recent occurrence
            }
        }
        
        // 3. Apply frequency-based scaling for frequently visited positions
        int visitCount = ctx.GetVisitCount(newPos);
        if (visitCount > 1)
        {   
            // Scale penalty based on how many times this position has been visited
            decimal frequencyScaling = Math.Min(MAX_FREQUENCY_SCALING, 1.0m + (visitCount / 10.0m));
            totalPenalty -= penaltyWeight * FREQUENCY_OSCILLATION_FACTOR * frequencyScaling;
        }
        
        // 4. Penalize direction reversal
        if (ctx.AnimalLastDirection.HasValue && 
            IsOppositeDirection(ctx.AnimalLastDirection.Value, ctx.CurrentMove))
        {   
            totalPenalty -= penaltyWeight * REVERSAL_OSCILLATION_FACTOR;
        }
        
        return totalPenalty;
    }

    /// <summary>
    /// Gets the oscillation penalty weight from HeuristicWeights, with fallback to ReverseMovePenalty
    /// </summary>
    private decimal GetWeightValue(HeuristicWeights weights)
    {   
        // Try to get OscillationPenalty property using reflection
        var property = typeof(HeuristicWeights).GetProperty("OscillationPenalty");
        if (property != null)
        {   
            var value = property.GetValue(weights);
            if (value != null)
            {   
                return Convert.ToDecimal(value);
            }
        }
        
        // Fallback to ReverseMovePenalty if OscillationPenalty is not available
        return weights.ReverseMovePenalty * 2.0m; // Double the reverse move penalty for stronger effect
    }

    /// <summary>
    /// Determines if two actions are opposite directions
    /// </summary>
    private bool IsOppositeDirection(BotAction a, BotAction b)
    {   
        return (a == BotAction.Left && b == BotAction.Right) ||
               (a == BotAction.Right && b == BotAction.Left) ||
               (a == BotAction.Up && b == BotAction.Down) ||
               (a == BotAction.Down && b == BotAction.Up);
    }
}
