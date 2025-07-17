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

            // Immediate danger zone (≤2 tiles).
            if (newDist <= 2)
            {
                decimal dangerFactor = 3 - newDist; // 2 when dist==1, 1 when dist==2
                score -= heuristicContext.Weights.CaptureAvoidancePenaltyFactor * dangerFactor;
                continue; // no reward for the same zookeeper in this case
            }

            if (newDist < currentDist)
            {
                // Moving closer – penalise proportionally to how much closer we get and proximity.
                int delta = currentDist - newDist;
                score -= heuristicContext.Weights.CaptureAvoidancePenaltyFactor * delta / newDist;
            }
            else if (newDist > currentDist)
            {
                // Moving away – reward inversely to original distance (closer escapes worth more).
                score += heuristicContext.Weights.CaptureAvoidanceRewardFactor / currentDist;
            }
        }

        // Clamp small magnitude near zero to exactly zero to avoid noise.
        if (score > -0.01m && score < 0.01m) return 0m;

        return score;
    }
}
