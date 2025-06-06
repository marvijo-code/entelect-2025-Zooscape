using System.Linq;
using ClingyHeuroBot2;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2;

public class ScoreLossMinimizerHeuristic : IHeuristic
{
    public string Name => "ScoreLossMinimizer";

    /// <summary>
    /// Calculates risk/reward considering score loss from capture
    /// </summary>
    public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
    {
        // Assume capture penalty is 50% of score (adjust based on actual game settings)
        float capturePenaltyPercent = 0.5f;

        // Calculate potential score loss if captured
        decimal potentialLoss = me.Score * (decimal)capturePenaltyPercent;

        var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);

        // Calculate capture risk based on zookeeper proximity
        decimal captureRisk = 0m;
        if (state.Zookeepers.Any())
        {
            int minDist = state.Zookeepers.Min(z => Heuristics.ManhattanDistance(z.X, z.Y, nx, ny));

            // Risk increases as zookeeper gets closer
            if (minDist <= 3)
                captureRisk = 0.8m / (minDist + 0.5m);
            else if (minDist <= 6)
                captureRisk = 0.3m / (minDist + 0.5m);
        }

        // Calculate risk-adjusted value
        decimal scoreThreshold = 20m; // Significant score worth protecting

        if (potentialLoss > scoreThreshold)
        {
            // Increase caution as potential loss increases
            return -captureRisk * (potentialLoss / 10m);
        }

        return -captureRisk * 0.5m; // Lower caution for small scores
    }
}
