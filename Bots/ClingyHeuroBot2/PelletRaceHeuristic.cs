#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using System.Linq;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace ClingyHeuroBot2
{
    public class PelletRaceHeuristic : IHeuristic
    {
        public string Name => "PelletRace";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (nx, ny) = HeuroBot.Services.Heuristics.HeuristicsImpl.ApplyMove(me.X, me.Y, move);
            var pellets = state.Cells.Where(c => c.Content == CellContent.Pellet);
            if (!pellets.Any())
                return 0m;
            var best = pellets
                .OrderBy(c =>
                    HeuroBot.Services.Heuristics.HeuristicsImpl.ManhattanDistance(nx, ny, c.X, c.Y)
                )
                .First();
            int myD = HeuroBot.Services.Heuristics.HeuristicsImpl.ManhattanDistance(
                nx,
                ny,
                best.X,
                best.Y
            );
            int minOther = state
                .Animals.Where(a => a.Id != me.Id && a.IsViable)
                .Select(a =>
                    HeuroBot.Services.Heuristics.HeuristicsImpl.ManhattanDistance(
                        a.X,
                        a.Y,
                        best.X,
                        best.Y
                    )
                )
                .DefaultIfEmpty(int.MaxValue)
                .Min();
            return minOther - myD >= 2 ? 1.2m : 0m;
        }
    }
}
