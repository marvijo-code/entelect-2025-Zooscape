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

        foreach (var zk in heuristicContext.CurrentGameState.Zookeepers)
        {
            int currentDist = BotUtils.ManhattanDistance(zk.X, zk.Y, heuristicContext.CurrentAnimal.X, heuristicContext.CurrentAnimal.Y);
            int newDist = BotUtils.ManhattanDistance(zk.X, zk.Y, nx, ny);

            // Fatal move – standing on the same tile.
            if (newDist == 0)
            {
                return -10000m;
            }

            // Check if we're moving closer or away from this zookeeper
            if (newDist < currentDist)
            {
                // Moving closer – penalise heavily, especially in danger zone
                decimal proximityMultiplier = newDist <= 2 ? (3 - newDist) : 1;
                int delta = currentDist - newDist;
                score -= heuristicContext.Weights.CaptureAvoidancePenaltyFactor * delta * proximityMultiplier;
            }
            else if (newDist > currentDist)
            {
                // Moving away – always reward, even if still in danger zone
                decimal escapeBonus = currentDist <= 2 ? heuristicContext.Weights.CaptureAvoidanceRewardFactor * 2 : heuristicContext.Weights.CaptureAvoidanceRewardFactor;
                score += escapeBonus / Math.Max(currentDist, 1);
            }
            else
            {
                // Same distance – only penalize if in immediate danger zone
                if (newDist <= 2)
                {
                    decimal dangerFactor = 3 - newDist; // 2 when dist==1, 1 when dist==2
                    score -= heuristicContext.Weights.CaptureAvoidancePenaltyFactor * dangerFactor * 0.5m; // Reduced penalty for staying same distance
                }
            }
        }

        // Clamp small magnitude near zero to exactly zero to avoid noise.
        if (score > -0.01m && score < 0.01m) return 0m;

        return score;
    }
}
