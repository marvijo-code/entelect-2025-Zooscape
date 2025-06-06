// #pragma warning disable SKEXP0110 // Disable SK Experimental warning
using System.Linq;
using HeuroBot.Services; // For Heuristics.HeuristicsImpl static methods
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2;

public class TargetEvaluationHeuristic : IHeuristic
{
    public string Name => "TargetEvaluation";

    public decimal CalculateRawScore(GameState state, Animal me, BotAction move, ILogger? logger)
    {
        logger?.Verbose("{Heuristic} not implemented", Name);
        return 0m;
    }
}
