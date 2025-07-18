#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;

namespace StaticHeuro.Heuristics;

public class CaptureAvoidanceHeuristic : IHeuristic
{
    public string Name => "CaptureAvoidance";

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
                return -20000m; // Increased penalty for fatal moves
            }
            else if (newDist == 1)
            {
                // Extremely dangerous - adjacent to zookeeper
                score -= heuristicContext.Weights.CaptureAvoidancePenaltyFactor * 5;
            }

            // Check if we're moving closer or away from this zookeeper
            if (newDist < currentDist)
            {
                // Moving closer – penalise heavily, especially in danger zone
                decimal proximityMultiplier = 1;
                
                // Enhanced danger zone logic - more aggressive scaling
                if (newDist <= 3) 
                {
                    proximityMultiplier = (4 - newDist) * 2; // 6 when dist==1, 4 when dist==2, 2 when dist==3
                }
                
                int delta = currentDist - newDist;
                score -= heuristicContext.Weights.CaptureAvoidancePenaltyFactor * delta * proximityMultiplier;
            }
            else if (newDist > currentDist)
            {
                // Moving away – always reward, especially from danger zone
                decimal escapeBonus = heuristicContext.Weights.CaptureAvoidanceRewardFactor;
                
                // Enhanced escape bonus when in danger
                if (currentDist <= 3) 
                {
                    escapeBonus = heuristicContext.Weights.CaptureAvoidanceRewardFactor * (4 - currentDist);
                }
                
                score += escapeBonus / Math.Max(currentDist, 1);
            }
            else
            {
                // Same distance – penalize more aggressively in danger zone
                if (newDist <= 3)
                {
                    decimal dangerFactor = 4 - newDist; // 3 when dist==1, 2 when dist==2, 1 when dist==3
                    score -= heuristicContext.Weights.CaptureAvoidancePenaltyFactor * dangerFactor * 0.75m; // Increased penalty
                }
            }
        }

        // Apply additional penalty if we're moving toward the closest zookeeper
        if (closestNewDist < closestCurrentDist && closestNewDist <= 5)
        {
            // Extra penalty for moving toward the closest zookeeper
            decimal extraPenalty = (6 - closestNewDist) * heuristicContext.Weights.CaptureAvoidancePenaltyFactor * 0.5m;
            score -= extraPenalty;
        }

        // Clamp small magnitude near zero to exactly zero to avoid noise.
        if (score > -0.01m && score < 0.01m) return 0m;

        return score;
    }
}
