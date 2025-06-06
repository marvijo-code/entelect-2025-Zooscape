#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics
{
    public class AnimalCongestionHeuristic : IHeuristic
    {
        public string Name => "AnimalCongestion";

        public decimal CalculateRawScore(
            GameState state,
            Animal me,
            BotAction move,
            ILogger? logger
        )
        {
            var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);
            var congestion = state.Animals.Count(a =>
                a.IsViable && a.Id != me.Id && Heuristics.ManhattanDistance(nx, ny, a.X, a.Y) <= 2
            );
            return -congestion * 0.5m;
        }
    }
}
