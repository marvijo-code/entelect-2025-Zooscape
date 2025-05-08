using MCTSo4.Models;

namespace MCTSo4.Algorithms.MCTS
{
    public static class MctsController
    {
        public static Move MCTS_GetBestAction(MCTSGameState state, BotParameters parameters)
        {
            // TODO: Integrate parameters (iterations, depth, exploration constant)
            return MctsAlgorithm.FindBestMove(state, parameters);
        }
    }
}
