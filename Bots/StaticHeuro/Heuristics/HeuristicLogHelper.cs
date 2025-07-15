using System;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

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
        if (logHeuristicScores && logger != null && componentContribution != 0m)
        {
            logger.Information(
                "    {HeuristicName,-40}: Raw={RawValue,8:F4}, Weight={Weight,8:F4}, Contribution={Contribution,10:F4}, NewScore={NewScore,10:F4}",
                heuristicName,
                rawValue, // Math.Round is not strictly needed here if F4 format specifier handles it, but Serilog might do its own rounding/truncation. Keeping for consistency if values are used elsewhere.
                weight,
                componentContribution,
                accumulatedScoreAfterThisComponent
            );
        }
    }
}
