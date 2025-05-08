using MCTSo4.Models;

namespace MCTSo4.Algorithms.MCTS
{
    /// <summary>
    /// Evaluates a game state using heuristic weights from BotParameters.
    /// </summary>
    public static class HeuristicFunction
    {
        public static double Evaluate(MCTSGameState state, BotParameters parameters)
        {
            if (state.IsTerminal())
                return state.Evaluate();
            // TODO: Combine state features with weights
            return 0.0;
        }
    }
}
