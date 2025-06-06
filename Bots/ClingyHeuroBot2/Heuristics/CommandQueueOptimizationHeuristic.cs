#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics
{
    public class CommandQueueOptimizationHeuristic : IHeuristic
    {
        public string Name => "CommandQueueOptimization";

        public decimal CalculateRawScore(
            GameState state,
            Animal me,
            BotAction move,
            ILogger? logger
        )
        {
            // This is a complex heuristic that would require analyzing a sequence of moves.
            // For now, it returns 0.
            logger?.Verbose("{Heuristic} not implemented", Name);
            return 0m;
        }
    }
}
