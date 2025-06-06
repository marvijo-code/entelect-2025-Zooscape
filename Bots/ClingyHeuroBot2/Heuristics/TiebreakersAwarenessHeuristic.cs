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

    public decimal CalculateRawScore(GameState state, Animal me, BotAction move, ILogger? logger)
    {
        var (nx, ny) = heuristicContext.MyNewPosition;
        decimal score = 0m;

        // Tiebreaker 1: Score (less is better)
        score -= me.Score * 0.01m;

        // Tiebreaker 2: Distance Covered (more is better)
        score += me.DistanceCovered * 0.01m;

        // Tiebreaker 3: Captured Counter (less is better)
        score -= me.CapturedCounter * 0.01m;

        return score;
    }
}
