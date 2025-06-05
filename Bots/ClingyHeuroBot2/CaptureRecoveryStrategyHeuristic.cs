using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace HeuroBot.Bots.ClingyHeuroBot2.Heuristics;

public class CaptureRecoveryStrategyHeuristic : IHeuristic
{
    public string Name => "CaptureRecoveryStrategy";

    public decimal CalculateRawScore(GameState state, Animal me, BotAction move, ILogger? logger)
    {
        // TODO: Implement original HeuristicsImpl.CaptureRecoveryStrategy logic from Heuristics.cs
        return 0m;
    }
}
