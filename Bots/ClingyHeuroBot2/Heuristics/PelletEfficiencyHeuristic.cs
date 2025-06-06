// #pragma warning disable SKEXP0110 // Disable SK Experimental warning
using System;
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class PelletEfficiencyHeuristic : IHeuristic
{
    public string Name => "PelletEfficiency";

    public decimal CalculateRawScore(IHeuristicContext heuristicContext)
    {
        heuristicContext.Logger?.Verbose("{Heuristic} not implemented", Name);
        return 0m;
    }
}
