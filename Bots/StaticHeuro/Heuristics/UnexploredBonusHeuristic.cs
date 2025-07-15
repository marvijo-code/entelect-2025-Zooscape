using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Models;

namespace ClingyHeuroBot2.Heuristics;

#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

public class UnexploredBonusHeuristic : IHeuristic
{
    public string Name => "UnexploredBonus";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        // TODO: Re-implement this heuristic after adding visit count to IHeuristicContext
        // var newPosition = heuristicContext.MyNewPosition;
        // int visits = heuristicContext.GetVisitCount(newPosition);
        //
        // if (visits == 0)
        // {
        //     return heuristicContext.Weights.UnexploredBonus;
        // }

        return 0m;
    }
}
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
