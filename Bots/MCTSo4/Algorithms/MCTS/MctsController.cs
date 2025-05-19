using System;
using System.Diagnostics;
using MCTSo4.Models;

namespace MCTSo4.Algorithms.MCTS
{
    public static class MctsController
    {
        public static Move MCTS_GetBestAction(
            MCTSGameState state,
            Guid botId,
            BotParameters parameters,
            Stopwatch stopwatch
        )
        {
            // TODO: Integrate parameters (iterations, depth, exploration constant)
            return MctsAlgorithm.FindBestMove(state, botId, parameters, stopwatch);
        }
    }
}
