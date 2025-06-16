#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class AnticipateCompetitionHeuristic : IHeuristic
{
    public string Name => "AnticipateCompetition";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition;
        var nearbyPellets = heuristicContext.CurrentGameState.Cells.Where(c =>
            c.Content == CellContent.Pellet && BotUtils.ManhattanDistance(nx, ny, c.X, c.Y) <= 3
        );
        var score = 0m;
        foreach (var pellet in nearbyPellets)
        {
            var competitors = heuristicContext.CurrentGameState.Animals.Where(a =>
                a.IsViable && a.Id != heuristicContext.CurrentAnimal.Id
            );
            var myDist = BotUtils.ManhattanDistance(nx, ny, pellet.X, pellet.Y);
            var theirDist = competitors.Any()
                ? competitors.Min(c => BotUtils.ManhattanDistance(c.X, c.Y, pellet.X, pellet.Y))
                : int.MaxValue;
            if (myDist < theirDist)
            {
                score += 0.2m;
            }
        }
        return score;
    }
}
