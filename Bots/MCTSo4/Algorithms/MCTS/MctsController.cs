using System.Diagnostics;
using MCTSo4.Models;

namespace MCTSo4.Algorithms.MCTS;

public static class MctsController
{
    // Flag to control whether to use parallel MCTS
    private static readonly bool UseParallelMCTS = true;

    public static Move MCTS_GetBestAction(
        MCTSGameState state,
        Guid botId,
        BotParameters parameters,
        Stopwatch stopwatch
    )
    {
        // Use parallel implementation if enabled
        if (UseParallelMCTS)
        {
            return ParallelMctsAlgorithm.FindBestMove(state, botId, parameters, stopwatch);
        }

        // Fall back to original implementation if parallel is disabled
        return MctsAlgorithm.FindBestMove(state, botId, parameters, stopwatch);
    }
}
