#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;

namespace StaticHeuro.Heuristics;

/// <summary>
/// Evaluates the risk of capture by zookeepers and applies penalties or rewards accordingly.
/// Balances safety with pellet collection opportunities.
/// </summary>
public class CaptureAvoidanceHeuristic : IHeuristic
{
    public string Name => "CaptureAvoidance";

    // Safety thresholds for different risk zones
    private const int IMMEDIATE_DANGER_THRESHOLD = 3;  // High risk zone
    private const int MODERATE_DANGER_THRESHOLD = 6;   // Medium risk zone
    private const int SAFE_DISTANCE_THRESHOLD = 10;    // Low risk zone

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition;

        // Bail out early if there are no zookeepers in the game state.
        if (!heuristicContext.CurrentGameState.Zookeepers.Any())
        {
            return 0m;
        }

        decimal score = 0m;

        // Track the closest zookeeper for enhanced danger assessment
        int closestCurrentDist = int.MaxValue;
        int closestNewDist = int.MaxValue;
        
        foreach (var zk in heuristicContext.CurrentGameState.Zookeepers)
        {
            int currentDist = BotUtils.ManhattanDistance(zk.X, zk.Y, heuristicContext.CurrentAnimal.X, heuristicContext.CurrentAnimal.Y);
            int newDist = BotUtils.ManhattanDistance(zk.X, zk.Y, nx, ny);
            
            // Track closest distances
            closestCurrentDist = Math.Min(closestCurrentDist, currentDist);
            closestNewDist = Math.Min(closestNewDist, newDist);

            // Fatal move – standing on the same tile or adjacent (high risk)
            if (newDist == 0)
            {
                return -20000m; // Absolute veto for fatal moves
            }
            else if (newDist == 1)
            {
                // Extremely dangerous - adjacent to zookeeper
                score -= heuristicContext.Weights.CaptureAvoidancePenaltyFactor * 5;
            }

            // Check if we're moving closer or away from this zookeeper
            if (newDist < currentDist)
            {
                // Moving closer – apply scaled penalty based on danger zone
                decimal proximityMultiplier = 1;
                
                // Scale penalty based on danger zone
                if (newDist <= IMMEDIATE_DANGER_THRESHOLD) 
                {
                    // High risk zone - aggressive penalty
                    proximityMultiplier = (IMMEDIATE_DANGER_THRESHOLD + 1 - newDist) * 1.5m;
                }
                else if (newDist <= MODERATE_DANGER_THRESHOLD)
                {
                    // Medium risk zone - moderate penalty
                    proximityMultiplier = 0.75m;
                }
                else if (newDist > SAFE_DISTANCE_THRESHOLD)
                {
                    // Safe distance - minimal penalty
                    proximityMultiplier = 0.25m;
                }
                
                int delta = currentDist - newDist;
                
                // Apply penalty with diminishing returns for distant zookeepers
                // This prevents pellet collection from being blocked by far-away zookeepers
                decimal penaltyFactor = Math.Min(
                    heuristicContext.Weights.CaptureAvoidancePenaltyFactor,
                    heuristicContext.Weights.CaptureAvoidancePenaltyFactor / (Math.Max(1, newDist - IMMEDIATE_DANGER_THRESHOLD))
                );
                
                score -= penaltyFactor * delta * proximityMultiplier;
            }
            else if (newDist > currentDist)
            {
                // Moving away – always reward, especially from danger zone
                decimal escapeBonus = heuristicContext.Weights.CaptureAvoidanceRewardFactor;
                
                // Enhanced escape bonus when in danger
                if (currentDist <= IMMEDIATE_DANGER_THRESHOLD) 
                {
                    escapeBonus = heuristicContext.Weights.CaptureAvoidanceRewardFactor * 
                                 (IMMEDIATE_DANGER_THRESHOLD + 1 - currentDist);
                }
                
                score += escapeBonus / Math.Max(currentDist, 1);
            }
            else
            {
                // Same distance – only penalize in danger zones
                if (newDist <= IMMEDIATE_DANGER_THRESHOLD)
                {
                    decimal dangerFactor = IMMEDIATE_DANGER_THRESHOLD + 1 - newDist;
                    score -= heuristicContext.Weights.CaptureAvoidancePenaltyFactor * dangerFactor * 0.5m;
                }
            }
        }

        // Apply additional penalty if we're moving toward the closest zookeeper
        // But only if we're already in or moving into a danger zone
        if (closestNewDist < closestCurrentDist && closestNewDist <= MODERATE_DANGER_THRESHOLD)
        {
            // Extra penalty for moving toward the closest zookeeper, scaled by distance
            decimal extraPenalty = Math.Min(
                (MODERATE_DANGER_THRESHOLD + 1 - closestNewDist) * heuristicContext.Weights.CaptureAvoidancePenaltyFactor * 0.3m,
                heuristicContext.Weights.CaptureAvoidancePenaltyFactor * 2
            );
            score -= extraPenalty;
        }

        // HARD VETO: Absolutely prohibit any move that ends within 3 tiles of a zookeeper
        // (distance 0-3 inclusive). This guarantees the bot never enters the critical
        // danger radius, regardless of any positive scoring bonuses (e.g. pellet).
        if (closestNewDist <= IMMEDIATE_DANGER_THRESHOLD)
        {
            // Pick a value that comfortably outweighs the maximum possible positive score
            // (ImmediatePelletBonus is 200 000), so we use ‑500 000 to ensure veto.
            return -500_000m;
        }

        // Clamp small magnitude near zero to exactly zero to avoid noise.
        if (score > -0.01m && score < 0.01m) return 0m;

        return score;
    }
}
