using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class ScoreLossMinimizerHeuristic : IHeuristic
{
    public string Name => "ScoreLossMinimizer";

    /// <summary>
    /// Calculates risk/reward considering score loss from capture
    /// </summary>
    public decimal CalculateRawScore(IHeuristicContext heuristicContext)
    {
        // Assume capture penalty is 50% of score (adjust based on actual game settings)
        float capturePenaltyPercent = 0.5f;

        // Calculate potential score loss if captured
        decimal potentialLoss =
            heuristicContext.CurrentAnimal.Score * (decimal)capturePenaltyPercent;

        var (nx, ny) = heuristicContext.MyNewPosition;

        // Calculate capture risk based on zookeeper proximity
        decimal captureRisk = 0m;
        if (heuristicContext.CurrentGameState.Zookeepers.Count > 0)
        {
            int minDist = heuristicContext.CurrentGameState.Zookeepers.Min(z =>
                Heuristics.ManhattanDistance(z.X, z.Y, nx, ny)
            );

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
