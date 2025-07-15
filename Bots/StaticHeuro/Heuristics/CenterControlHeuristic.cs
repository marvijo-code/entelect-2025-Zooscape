#pragma warning disable SKEXP0110 // Added
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils; // Added
using Serilog;

namespace StaticHeuro.Heuristics;

public class CenterControlHeuristic : IHeuristic
{
    public string Name => "CenterControl";

    public decimal CalculateScore(IHeuristicContext heuristicContext) // Updated signature
    {
        int cx =
            (
                heuristicContext.CurrentGameState.Cells.Min(c => c.X)
                + heuristicContext.CurrentGameState.Cells.Max(c => c.X)
            ) / 2; // Updated
        int cy =
            (
                heuristicContext.CurrentGameState.Cells.Min(c => c.Y)
                + heuristicContext.CurrentGameState.Cells.Max(c => c.Y)
            ) / 2; // Updated
        var (nx, ny) = heuristicContext.MyNewPosition;
        int distToCenter = BotUtils.ManhattanDistance(nx, ny, cx, cy); // Updated

        return distToCenter <= 2 ? 0.3m : 0m;
    }
}
