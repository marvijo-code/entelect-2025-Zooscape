#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils; // Added
using Serilog;

namespace ClingyHeuroBot2.Heuristics
{
    public class AnimalCongestionHeuristic : IHeuristic
    {
        public string Name => "AnimalCongestion";

        public decimal CalculateRawScore(IHeuristicContext heuristicContext)
        {
            var (nx, ny) = heuristicContext.MyNewPosition; // Updated
            var congestion = heuristicContext.CurrentGameState.Animals.Count(a =>
                a.IsViable
                && a.Id != heuristicContext.CurrentAnimal.Id
                && BotUtils.ManhattanDistance(nx, ny, a.X, a.Y) <= 2 // Updated
            );
            return -congestion * 0.5m;
        }
    }
}
