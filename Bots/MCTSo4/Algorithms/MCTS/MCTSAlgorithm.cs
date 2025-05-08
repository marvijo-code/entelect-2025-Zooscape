using System;
using System.Collections.Generic;
using System.Linq;
using MCTSo4.Enums;
using MCTSo4.Models;

namespace MCTSo4.Algorithms.MCTS
{
    /// <summary>
    /// Monte Carlo Tree Search implementation.
    /// </summary>
    public static class MctsAlgorithm
    {
        private static readonly Random Rnd = new Random();

        public static Move FindBestMove(MCTSGameState rootState, BotParameters parameters)
        {
            // Initialize root node
            var root = new MctsNode(rootState.Clone(), null, null, parameters.ExplorationConstant);
            // Run MCTS iterations
            for (int i = 0; i < parameters.MctsIterations; i++)
            {
                // Selection
                var node = root;
                while (!node.IsTerminal && node.IsFullyExpanded)
                    node = node.BestChild();

                // Expansion
                if (!node.IsTerminal && node.UntriedActions.Any())
                {
                    var actionIndex = Rnd.Next(node.UntriedActions.Count);
                    var action = node.UntriedActions[actionIndex];
                    node.UntriedActions.RemoveAt(actionIndex);
                    var nextState = node.State.Apply(action);
                    var child = new MctsNode(
                        nextState,
                        node,
                        action,
                        parameters.ExplorationConstant
                    );
                    node.Children.Add(child);
                    node = child;
                }

                // Simulation (Rollout)
                var rolloutState = node.State.Clone();
                while (!rolloutState.IsTerminal())
                {
                    var legal = rolloutState.GetLegalMoves();
                    var randomAction = legal[Rnd.Next(legal.Count)];
                    rolloutState = rolloutState.Apply(randomAction);
                }
                var reward = rolloutState.Evaluate();

                // Backpropagation
                while (node != null)
                {
                    node.Visits++;
                    node.Wins += reward;
                    node = node.Parent;
                }
            }

            // Choose best move: highest visit count
            var bestChild = root.Children.OrderByDescending(c => c.Visits).FirstOrDefault();
            return bestChild?.Move ?? root.UntriedActions.First();
        }
    }
}
