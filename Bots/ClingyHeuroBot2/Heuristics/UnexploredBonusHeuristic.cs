using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Models;

namespace ClingyHeuroBot2.Heuristics;

#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

public class UnexploredBonusHeuristic : IHeuristic
{
    public string Name => "UnexploredBonus";

    public decimal CalculateRawScore(IHeuristicContext heuristicContext)
    {
        var newPosition = heuristicContext.MyNewPosition;
        int visits = heuristicContext.GetVisitCount(newPosition);

        if (visits == 0)
        {
            // WEIGHTS.UnexploredBonus is the raw score value for finding an unexplored cell.
            // The actual weight multiplier for this heuristic will be defined in WEIGHTS.cs
            // and applied by the HeuristicsManager.
            return WEIGHTS.UnexploredBonus;
        }

        return 0m;
    }
}
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
