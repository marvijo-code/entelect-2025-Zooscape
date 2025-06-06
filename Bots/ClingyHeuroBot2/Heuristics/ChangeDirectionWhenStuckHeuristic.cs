#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics
{
    public class ChangeDirectionWhenStuckHeuristic : IHeuristic
    {
        public string Name => "ChangeDirectionWhenStuck";

        public decimal CalculateRawScore(
            GameState state,
            Animal me,
            BotAction move,
            ILogger? logger
        )
        {
            // This heuristic requires state about the last few moves, which is managed in HeuristicsManager.
            // For now, we return 0, assuming the core logic will be in the manager.
            // A more advanced implementation could take a history of moves as a parameter.
            logger?.Verbose(
                "{Heuristic} logic is dependent on move history managed elsewhere.",
                Name
            );
            return 0m;
        }
    }
}
