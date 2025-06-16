#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class ResourceClusteringHeuristic : IHeuristic
{
    public string Name => "ResourceClustering";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition;

        if (heuristicContext.CurrentGameState.Cells == null)
            return 0m;

        int pelletCount = heuristicContext.CurrentGameState.Cells.Count(c =>
            c.Content == CellContent.Pellet && BotUtils.ManhattanDistance(c.X, c.Y, nx, ny) <= 3
        );

        int immediatePellets = heuristicContext.CurrentGameState.Cells.Count(c =>
            c.Content == CellContent.Pellet && BotUtils.ManhattanDistance(c.X, c.Y, nx, ny) <= 1
        );

        return (pelletCount * heuristicContext.Weights.ResourceClusteringBonus) + (immediatePellets * heuristicContext.Weights.ResourceClusteringImmediatePelletBonus);
    }
}
