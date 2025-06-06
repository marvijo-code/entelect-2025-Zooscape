#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class PelletRaceHeuristic : IHeuristic
{
    public string Name => "PelletRace";

    public decimal CalculateRawScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition;
        var pellets = heuristicContext.CurrentGameState.Cells.Where(c =>
            c.Content == CellContent.Pellet
        );
        if (!pellets.Any())
            return 0m;
        var best = pellets.OrderBy(c => Heuristics.ManhattanDistance(nx, ny, c.X, c.Y)).First();
        int myD = Heuristics.ManhattanDistance(nx, ny, best.X, best.Y);
        int minOther = heuristicContext
            .CurrentGameState.Animals.Where(a =>
                a.Id != heuristicContext.CurrentAnimal.Id && a.IsViable
            )
            .Select(a => Heuristics.ManhattanDistance(a.X, a.Y, best.X, best.Y))
            .DefaultIfEmpty(int.MaxValue)
            .Min();
        return minOther - myD >= 2 ? 1.2m : 0m;
    }
}
