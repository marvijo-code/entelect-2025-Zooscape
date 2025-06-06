// #pragma warning disable SKEXP0110 // Disable SK Experimental warning
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class TargetEvaluationHeuristic : IHeuristic
{
    public string Name => "TargetEvaluation";

    public decimal CalculateRawScore(IHeuristicContext heuristicContext)
    {
        logger?.Verbose("{Heuristic} not implemented", Name);
        return 0m;
    }
}
