#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Utils;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace StaticHeuro.Heuristics;

public class PelletRaceHeuristic : IHeuristic
{
    public string Name => "PelletRace";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition;
        // Find closest pellet without expensive LINQ operations
        Cell? bestPellet = null;
        int bestDistance = int.MaxValue;
        
        foreach (var cell in heuristicContext.CurrentGameState.Cells)
        {
            if (cell.Content != CellContent.Pellet) continue;
            
            int distance = BotUtils.ManhattanDistance(nx, ny, cell.X, cell.Y);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPellet = cell;
            }
        }
        
        if (bestPellet == null) return 0m;
        int myD = BotUtils.ManhattanDistance(nx, ny, bestPellet.X, bestPellet.Y);
        
        // Find minimum opponent distance without LINQ
        int minOther = int.MaxValue;
        foreach (var animal in heuristicContext.CurrentGameState.Animals)
        {
            if (animal.Id == heuristicContext.CurrentAnimal.Id || !animal.IsViable) continue;
            
            int distance = BotUtils.ManhattanDistance(animal.X, animal.Y, bestPellet.X, bestPellet.Y);
            if (distance < minOther)
            {
                minOther = distance;
            }
        }
        return minOther - myD >= 2 ? heuristicContext.Weights.PelletRace : 0m;
    }
}
