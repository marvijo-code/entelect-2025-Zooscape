#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;

namespace StaticHeuro.Heuristics;

public class TimeToCaptureHeuristic : IHeuristic
{
    public string Name => "TimeToCapture";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition;

        if (!heuristicContext.CurrentGameState.Zookeepers.Any())
        {
            return 0m;
        }

        int distToZookeeper = heuristicContext.CurrentGameState.Zookeepers
            .Min(z => BotUtils.ManhattanDistance(z.X, z.Y, nx, ny));

        if (distToZookeeper <= 2)
        {
            return -heuristicContext.Weights.TimeToCaptureDanger * (3 - distToZookeeper);
        }

        return distToZookeeper * heuristicContext.Weights.TimeToCaptureSafety;
    }
}
