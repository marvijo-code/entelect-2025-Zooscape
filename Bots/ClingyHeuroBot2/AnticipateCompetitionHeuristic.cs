using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace HeuroBot.Bots.ClingyHeuroBot2.Heuristics;

public class AnticipateCompetitionHeuristic : IHeuristic
{
    public string Name => "AnticipateCompetition";

    public decimal CalculateRawScore(GameState state, Animal me, BotAction move, ILogger? logger)
    {
        // TODO: Implement original HeuristicsImpl.AnticipateCompetition logic from Heuristics.cs
        return 0m;
    }
}
