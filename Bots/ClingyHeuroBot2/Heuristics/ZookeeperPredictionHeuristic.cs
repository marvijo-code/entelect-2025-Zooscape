#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class ZookeeperPredictionHeuristic : IHeuristic
{
    public string Name => "ZookeeperPrediction";

    public decimal CalculateRawScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition;

        // Simplified implementation - predict zookeepers will move toward closest animal
        var zookeepers = state.Zookeepers.ToList();
        if (!zookeepers.Any())
            return 0m;

        foreach (var zookeeper in zookeepers)
        {
            // Simple prediction: zookeeper moves toward closest animal
            var closestAnimal = state
                .Animals.OrderBy(a =>
                    Heuristics.ManhattanDistance(a.X, a.Y, zookeeper.X, zookeeper.Y)
                )
                .FirstOrDefault();
            if (closestAnimal != null)
            {
                // Predict zookeeper will move one step toward closest animal
                int dx =
                    closestAnimal.X > zookeeper.X ? 1 : (closestAnimal.X < zookeeper.X ? -1 : 0);
                int dy =
                    closestAnimal.Y > zookeeper.Y ? 1 : (closestAnimal.Y < zookeeper.Y ? -1 : 0);
                int predictedX = zookeeper.X + dx;
                int predictedY = zookeeper.Y + dy;

                if (predictedX == nx && predictedY == ny)
                    return -3.0m; // Strong penalty for moving into predicted zookeeper position

                int distToPredicted = Heuristics.ManhattanDistance(predictedX, predictedY, nx, ny);
                if (distToPredicted <= 2)
                    return -1.5m / ((decimal)distToPredicted + 0.5m);
            }
        }

        return 0m;
    }
}
