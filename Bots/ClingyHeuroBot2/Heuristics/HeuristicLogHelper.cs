using System;
using Serilog;

namespace ClingyHeuroBot2;

public class HeuristicLogHelper
{
    public void LogScoreComponent(
        ILogger? logger,
        bool logHeuristicScores,
        string heuristicName,
        decimal rawValue,
        decimal weight,
        decimal componentContribution,
        decimal accumulatedScoreAfterThisComponent
    )
    {
        if (logHeuristicScores && logger != null)
        {
            logger.Information(
                "    {HeuristicName}: Raw={RawValue}, Weight={Weight}, Contribution={Contribution}, NewScore={NewScore}",
                heuristicName,
                Math.Round(rawValue, 4),
                Math.Round(weight, 4),
                Math.Round(componentContribution, 4),
                Math.Round(accumulatedScoreAfterThisComponent, 4)
            );
        }
    }
}
