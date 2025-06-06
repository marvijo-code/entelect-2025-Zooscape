#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class TiebreakersAwarenessHeuristic : IHeuristic
{
    public string Name => "TiebreakersAwareness";

    public decimal CalculateRawScore(IHeuristicContext context)
    {
        // var (nx, ny) = context.MyNewPosition; // Not used in current logic but available
        decimal score = 0m;

        // Tiebreaker 1: Score (less is better)
        score -= context.CurrentAnimal.Score * 0.01m;

        // Tiebreaker 2: Distance Covered (more is better)
        score += context.CurrentAnimal.DistanceCovered * 0.01m;

        // Tiebreaker 3: Captured Counter (less is better)
        score -= context.CurrentAnimal.CapturedCounter * 0.01m;

        context.Logger?.Verbose(
            "{Heuristic}: Calculated tiebreaker score {Score} for move {Move} to ({NewX}, {NewY})",
            Name,
            score,
            context.CurrentMove,
            context.MyNewPosition.X,
            context.MyNewPosition.Y
        );

        return score;
    }
}
