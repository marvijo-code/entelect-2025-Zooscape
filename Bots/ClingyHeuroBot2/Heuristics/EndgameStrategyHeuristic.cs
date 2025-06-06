using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class EndgameStrategyHeuristic : IHeuristic
{
    public string Name => "EndgameStrategy";

    public decimal CalculateRawScore(GameState state, Animal me, BotAction move, ILogger? logger)
    {
        logger?.Verbose("{Heuristic} not implemented", Name);
        return 0m;
    }
}
