#pragma warning disable SKEXP0110
using System.Linq;
using ClingyHeuroBot2;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class AnticipateCompetitionHeuristic : IHeuristic
{
    public string Name => "AnticipateCompetition";

    public decimal CalculateRawScore(GameState state, Animal me, BotAction move, ILogger? logger)
    {
        var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);
        var nearbyPellets = state.Cells.Where(c =>
            c.Content == CellContent.Pellet && Heuristics.ManhattanDistance(nx, ny, c.X, c.Y) <= 3
        );
        var score = 0m;
        foreach (var pellet in nearbyPellets)
        {
            var competitors = state.Animals.Where(a => a.IsViable && a.Id != me.Id);
            var myDist = Heuristics.ManhattanDistance(nx, ny, pellet.X, pellet.Y);
            var theirDist = competitors.Any()
                ? competitors.Min(c => Heuristics.ManhattanDistance(c.X, c.Y, pellet.X, pellet.Y))
                : int.MaxValue;
            if (myDist < theirDist)
            {
                score += 0.2m;
            }
        }
        return score;
    }
}
