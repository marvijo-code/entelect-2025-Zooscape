#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils; // Added
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class DensityMappingHeuristic : IHeuristic
{
    public string Name => "DensityMapping";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition; // Updated
        var density = heuristicContext.CurrentGameState.Cells.Count(c =>
            c.Content == CellContent.Pellet && BotUtils.ManhattanDistance(nx, ny, c.X, c.Y) <= 3 // Updated
        );
        return density * 0.1m;
    }
}
