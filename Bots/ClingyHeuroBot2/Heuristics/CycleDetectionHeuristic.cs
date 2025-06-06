#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics
{
    public class CycleDetectionHeuristic : IHeuristic
    {
        public string Name => "CycleDetection";

        public decimal CalculateRawScore(
            GameState state,
            Animal me,
            BotAction move,
            ILogger? logger
        )
        {
            var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);
            string animalKey = me.Id.ToString();

            if (!HeuristicsManager._recentPositions.ContainsKey(animalKey))
                return 0m;

            var positions = HeuristicsManager._recentPositions[animalKey];

            if (positions.Count < 3)
                return 0m;

            int visitCount = positions.Count(p => p.Item1 == nx && p.Item2 == ny);

            if (visitCount >= 2)
                return -3.0m * visitCount;

            if (positions.Count >= 4)
            {
                var positionList = positions.ToList();
                for (int i = 0; i < positionList.Count - 3; i += 2)
                {
                    if (
                        positionList[i].Item1 == positionList[i + 2].Item1
                        && positionList[i].Item2 == positionList[i + 2].Item2
                        && positionList[i + 1].Item1 == positionList[i + 3].Item1
                        && positionList[i + 1].Item2 == positionList[i + 3].Item2
                    )
                    {
                        if (
                            (nx == positionList[i].Item1 && ny == positionList[i].Item2)
                            || (nx == positionList[i + 1].Item1 && ny == positionList[i + 1].Item2)
                        )
                            return -4.0m;
                    }
                }
            }

            return 0m;
        }
    }
}
