#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace StaticHeuro.Heuristics;

public class TiebreakersAwarenessHeuristic : IHeuristic
{
    public string Name => "TiebreakersAwareness";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        // var (nx, ny) = context.MyNewPosition; // Not used in current logic but available
        decimal score = 0m;

        // Tiebreaker 1: Score (less is better)
        score -= heuristicContext.CurrentAnimal.Score * heuristicContext.Weights.TiebreakerScore;

        // Tiebreaker 2: Distance Covered (more is better)
        score += heuristicContext.CurrentAnimal.DistanceCovered * heuristicContext.Weights.TiebreakerDistance;

        // Tiebreaker 3: Captured Counter (less is better)
        score -= heuristicContext.CurrentAnimal.CapturedCounter * heuristicContext.Weights.TiebreakerCaptured;

        heuristicContext.Logger?.Verbose(
            "{Heuristic}: Calculated tiebreaker score {Score} for move {Move} to ({NewX}, {NewY})",
            Name,
            score,
            heuristicContext.CurrentMove,
            heuristicContext.MyNewPosition.X,
            heuristicContext.MyNewPosition.Y
        );

        return score;
    }
}
