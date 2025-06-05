using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace HeuroBot.Bots.ClingyHeuroBot2.Heuristics;

public class ChangeDirectionWhenStuckHeuristic : IHeuristic
{
    public string Name => "ChangeDirectionWhenStuck";

    public decimal CalculateRawScore(GameState state, Animal me, BotAction move, ILogger? logger)
    {
        // TODO: Implement original HeuristicsImpl.ChangeDirectionWhenStuck logic from Heuristics.cs
        return 0m;
    }
}
