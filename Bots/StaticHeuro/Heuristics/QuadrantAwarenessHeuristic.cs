#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace StaticHeuro.Heuristics;

public class QuadrantAwarenessHeuristic : IHeuristic
{
    public string Name => "QuadrantAwareness";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition;

        if (!heuristicContext.CurrentGameState.Cells.Any())
            return 0m; // Should not happen

        // Cache board bounds to avoid repeated LINQ operations
        var cells = heuristicContext.CurrentGameState.Cells;
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (var cell in cells)
        {
            if (cell.X < minX) minX = cell.X;
            if (cell.X > maxX) maxX = cell.X;
            if (cell.Y < minY) minY = cell.Y;
            if (cell.Y > maxY) maxY = cell.Y;
        }

        // Integer division is fine for determining center for quadrant logic
        int centerX = (minX + maxX) / 2;
        int centerY = (minY + maxY) / 2;

        // Determine the quadrant of the new position (nx, ny)
        // Quadrant mapping:
        // 0: bottom-left (x < centerX, y < centerY)
        // 1: bottom-right (x >= centerX, y < centerY)
        // 2: top-left (x < centerX, y >= centerY)
        // 3: top-right (x >= centerX, y >= centerY)
        int targetQuadrant = (nx >= centerX ? 1 : 0) + (ny >= centerY ? 2 : 0);

        int[] pelletsByQuadrant = new int[4];
        foreach (var cell in cells)
        {
            if (cell.Content != CellContent.Pellet) continue;
            int q = (cell.X >= centerX ? 1 : 0) + (cell.Y >= centerY ? 2 : 0);
            pelletsByQuadrant[q]++;
        }

        int[] animalsByQuadrant = new int[4];
        foreach (var animal in heuristicContext.CurrentGameState.Animals)
        {
            if (animal.Id == heuristicContext.CurrentAnimal.Id || !animal.IsViable) continue;
            int q = (animal.X >= centerX ? 1 : 0) + (animal.Y >= centerY ? 2 : 0);
            animalsByQuadrant[q]++;
        }

        decimal quadrantValue = pelletsByQuadrant[targetQuadrant] * heuristicContext.Weights.QuadrantPelletBonus;
        if (animalsByQuadrant[targetQuadrant] > 0)
        {
            quadrantValue -= animalsByQuadrant[targetQuadrant] * heuristicContext.Weights.QuadrantAnimalPenalty;
        }

        return quadrantValue;
    }
}
