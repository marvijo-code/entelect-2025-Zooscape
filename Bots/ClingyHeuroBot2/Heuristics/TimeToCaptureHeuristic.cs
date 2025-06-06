#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class TimeToCaptureHeuristic : IHeuristic
{
    public string Name => "TimeToCapture";

    public decimal CalculateRawScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition;

        if (!state.Zookeepers.Any())
        {
            return 0m;
        }

        var zookeeper = state
            .Zookeepers.OrderBy(z => Heuristics.ManhattanDistance(z.X, z.Y, nx, ny))
            .First();
        int distToZookeeper = Heuristics.ManhattanDistance(zookeeper.X, zookeeper.Y, nx, ny);

        if (distToZookeeper <= 2)
        {
            return -5.0m * (3 - distToZookeeper);
        }

        return (decimal)distToZookeeper * 0.5m;
    }
}
