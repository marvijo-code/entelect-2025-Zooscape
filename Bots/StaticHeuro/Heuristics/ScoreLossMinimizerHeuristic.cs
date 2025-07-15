using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;

namespace StaticHeuro.Heuristics;

public class ScoreLossMinimizerHeuristic : IHeuristic
{
    public string Name => "ScoreLossMinimizer";

    /// <summary>
    /// Calculates risk/reward considering score loss from capture
    /// </summary>
    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        // Assume capture penalty is based on game settings
        var capturePenaltyPercent = heuristicContext.Weights.CapturePenaltyPercent;

        // Calculate potential score loss if captured
        decimal potentialLoss =
            heuristicContext.CurrentAnimal.Score * capturePenaltyPercent;

        var (nx, ny) = heuristicContext.MyNewPosition;

        // Calculate capture risk based on zookeeper proximity
        decimal captureRisk = 0m;
        if (heuristicContext.CurrentGameState.Zookeepers.Count > 0)
        {
            int minDist = heuristicContext.CurrentGameState.Zookeepers.Min(z =>
                BotUtils.ManhattanDistance(z.X, z.Y, nx, ny)
            );

            // Prevent division by zero: ensure configured divisor is not zero
            decimal riskDistanceDivisor = heuristicContext.Weights.ScoreLossMinimizerRiskDistanceDivisor;
            if (riskDistanceDivisor == 0m)
            {
                heuristicContext.Logger?.Debug("{HeuristicName}: ScoreLossMinimizerRiskDistanceDivisor is zero. Using fallback divisor of 1 to avoid division by zero.", Name);
                riskDistanceDivisor = 1m; // Fallback to 1 to avoid divide-by-zero
            }

            // Risk increases as zookeeper gets closer
            if (minDist <= heuristicContext.Weights.ScoreLossMinimizerHighRiskDistance)
                captureRisk = heuristicContext.Weights.ScoreLossMinimizerHighRiskFactor /
                              (minDist + riskDistanceDivisor);
            else if (minDist <= heuristicContext.Weights.ScoreLossMinimizerMediumRiskDistance)
                captureRisk = heuristicContext.Weights.ScoreLossMinimizerMediumRiskFactor /
                              (minDist + riskDistanceDivisor);
        }

        // Calculate risk-adjusted value
        var scoreThreshold = heuristicContext.Weights.ScoreLossMinimizerSignificantScoreThreshold;

        if (potentialLoss > scoreThreshold)
        {
            // Increase caution as potential loss increases
            if (heuristicContext.Weights.ScoreLossMinimizerCautionFactor == 0m)
            {
                heuristicContext.Logger?.Warning("{HeuristicName}: ScoreLossMinimizerCautionFactor is zero, cannot apply caution scaling. Returning -captureRisk.", Name);
                return -captureRisk; // Avoid division by zero, return risk without caution scaling
            }
            return -captureRisk * (potentialLoss / heuristicContext.Weights.ScoreLossMinimizerCautionFactor);
        }

        return -captureRisk * heuristicContext.Weights.ScoreLossMinimizerLowScoreCautionFactor;
    }
}
