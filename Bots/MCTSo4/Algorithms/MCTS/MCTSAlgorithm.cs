using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MCTSo4.Enums;
using MCTSo4.Models;
using Serilog;

namespace MCTSo4.Algorithms.MCTS
{
    /// <summary>
    /// Monte Carlo Tree Search implementation.
    /// </summary>
    public static class MctsAlgorithm
    {
        private static readonly Random Rnd = new Random();
        private static readonly ILogger AlgoLog = Log.ForContext(typeof(MctsAlgorithm));

        public static Move FindBestMove(
            MCTSGameState rootState,
            Guid botId,
            BotParameters parameters,
            Stopwatch stopwatch
        )
        {
            if (botId == Guid.Empty)
            {
                AlgoLog.Error(
                    "FindBestMove called with Guid.Empty BotId. MCTS cannot proceed. Returning a random move."
                );
                var legalMoves = rootState.GetLegalMoves();
                return legalMoves.Any() ? legalMoves[Rnd.Next(legalMoves.Count)] : Move.Up; // Default to Up if no legal moves somehow
            }

            AlgoLog.Debug(
                "FindBestMove called for BotId: {BotId}. MctsIterations: {Iterations}, MctsDepth: {Depth}, Exploration: {Exploration}",
                botId,
                parameters.MctsIterations,
                parameters.MctsDepth,
                parameters.ExplorationConstant
            );
            var root = new MctsNode(
                rootState.Clone(),
                null,
                null,
                parameters.ExplorationConstant,
                botId,
                parameters
            );

            for (int i = 0; i < parameters.MctsIterations; i++)
            {
                if (stopwatch.ElapsedMilliseconds >= 140)
                {
                    AlgoLog.Warning(
                        "MCTS terminating iteration {Iteration} for BotId {BotId} early due to time limit ({ElapsedMs}ms >= 140ms)",
                        i + 1,
                        botId,
                        stopwatch.ElapsedMilliseconds
                    );
                    break;
                }
                AlgoLog.Verbose(
                    "MCTS Iteration {Iteration}/{TotalIterations} for BotId {BotId} starting...",
                    i + 1,
                    parameters.MctsIterations,
                    botId
                );

                var node = root;
                int selectionTreeDepth = 0;

                AlgoLog.Verbose("Starting Selection phase for BotId {BotId}...", botId);
                while (!node.IsTerminal(0, int.MaxValue) && node.IsFullyExpanded)
                {
                    node = node.BestChild();
                    AlgoLog.Verbose(
                        "Selected node: {NodeAction}, Visits: {Visits}, Wins: {Wins} for BotId {BotId}",
                        node.Move,
                        node.Visits,
                        node.Wins,
                        botId
                    );
                    selectionTreeDepth++;
                }
                AlgoLog.Verbose(
                    "Selection phase complete for BotId {BotId}. Selected node leads with action: {Action}",
                    botId,
                    node.Move
                );

                // Log before checking expansion conditions
                bool rootIsTerminalCheck = node.IsTerminal(0, int.MaxValue); // Assuming node is root here if i=0 and selection didn't move
                bool rootHasUntriedActionsCheck = node.UntriedActions.Any();
                AlgoLog.Debug(
                    "Pre-Expansion Check for BotId {BotId}: Node isRoot? {IsRoot}, IsTerminal(0,inf)? {IsTerminal}, HasUntriedActions? {HasUntried}, UntriedCount: {UntriedCount}",
                    botId,
                    node == root,
                    rootIsTerminalCheck,
                    rootHasUntriedActionsCheck,
                    node.UntriedActions.Count
                );

                AlgoLog.Verbose("Starting Expansion phase for BotId {BotId}...", botId);
                if (!rootIsTerminalCheck && rootHasUntriedActionsCheck) // Use the checked values
                {
                    AlgoLog.Debug(
                        "Expansion criteria met for BotId {BotId}. Expanding node.",
                        botId
                    );
                    var actionIndex = Rnd.Next(node.UntriedActions.Count);
                    var actionToExpand = node.UntriedActions[actionIndex];
                    node.UntriedActions.RemoveAt(actionIndex);
                    var nextState = node.State.Apply(actionToExpand, botId);
                    var child = new MctsNode(
                        nextState,
                        node,
                        actionToExpand,
                        parameters.ExplorationConstant,
                        botId,
                        parameters
                    );
                    node.Children.Add(child);
                    node = child;
                    AlgoLog.Verbose(
                        "Expanded with action: {Action} for BotId {BotId}. New node created.",
                        actionToExpand,
                        botId
                    );
                }
                else
                {
                    AlgoLog.Verbose(
                        "Expansion phase for BotId {BotId}: Node is terminal or no untried actions. Current node action: {Action}",
                        botId,
                        node.Move
                    );
                }
                AlgoLog.Verbose("Expansion phase complete for BotId {BotId}.", botId);

                AlgoLog.Verbose(
                    "Starting Simulation (Rollout) phase from node with action: {Action} for BotId {BotId}...",
                    node.Move,
                    botId
                );
                var rolloutState = node.State.Clone();
                int rolloutStepCount = 0;

                for (
                    rolloutStepCount = 0;
                    rolloutStepCount < parameters.MctsDepth;
                    rolloutStepCount++
                )
                {
                    if (
                        rolloutState.IsTerminal(
                            botId,
                            parameters,
                            rolloutStepCount,
                            parameters.MctsDepth
                        )
                    )
                    {
                        AlgoLog.Verbose(
                            "Rollout for BotId {BotId} reached a terminal state at step {Step} out of max depth {MaxDepth}.",
                            botId,
                            rolloutStepCount,
                            parameters.MctsDepth
                        );
                        break;
                    }

                    var legalMoves = rolloutState.GetLegalMoves();
                    if (!legalMoves.Any())
                    {
                        AlgoLog.Warning(
                            "Rollout for BotId {BotId} in iteration {Iteration}, step {Step}: No legal moves found but state is not terminal. Breaking rollout.",
                            botId,
                            i + 1,
                            rolloutStepCount
                        );
                        break;
                    }
                    var randomAction = legalMoves[Rnd.Next(legalMoves.Count)];
                    rolloutState = rolloutState.Apply(randomAction, botId);
                }

                if (
                    rolloutStepCount == parameters.MctsDepth
                    && !rolloutState.IsTerminal(
                        botId,
                        parameters,
                        rolloutStepCount,
                        parameters.MctsDepth
                    )
                )
                {
                    AlgoLog.Verbose(
                        "Rollout for BotId {BotId} reached max depth {MaxDepth} without reaching a terminal state.",
                        botId,
                        parameters.MctsDepth
                    );
                }

                var reward = rolloutState.Evaluate(botId, parameters);
                AlgoLog.Debug(
                    "Simulation (Rollout) for BotId {BotId} complete after {Steps} steps (max depth {MaxDepth}). Reward: {Reward}. Final state IsTerminal: {IsTerminalNow}",
                    botId,
                    rolloutStepCount,
                    parameters.MctsDepth,
                    reward,
                    rolloutState.IsTerminal(
                        botId,
                        parameters,
                        rolloutStepCount,
                        parameters.MctsDepth
                    )
                );

                AlgoLog.Verbose("Starting Backpropagation phase for BotId {BotId}...", botId);
                var tempNode = node;
                while (tempNode != null)
                {
                    tempNode.Visits++;
                    tempNode.Wins += reward;
                    AlgoLog.Verbose(
                        "Backpropagated to node: {NodeAction}, NewVisits: {Visits}, NewWins: {Wins} for BotId {BotId}",
                        tempNode.Move,
                        tempNode.Visits,
                        tempNode.Wins,
                        botId
                    );
                    tempNode = tempNode.Parent;
                }
                AlgoLog.Verbose("Backpropagation phase complete for BotId {BotId}.", botId);
                AlgoLog.Debug(
                    "MCTS Iteration {Iteration} for BotId {BotId} complete. Time elapsed: {ElapsedMs}ms",
                    i + 1,
                    botId,
                    stopwatch.ElapsedMilliseconds
                );
            }

            if (!root.Children.Any())
            {
                AlgoLog.Warning(
                    "MCTS for BotId {BotId}: Root node has no children after all iterations. Root untried actions count: {Count}",
                    botId,
                    root.UntriedActions.Count
                );
                if (root.UntriedActions.Any())
                {
                    AlgoLog.Information(
                        "BotId {BotId}: Falling back to first untried action from root due to no children.",
                        botId
                    );
                    return root.UntriedActions.First();
                }
                else if (root.State.GetLegalMoves().Any())
                {
                    AlgoLog.Information(
                        "BotId {BotId}: Falling back to first legal move from root state as there are no untried actions or children.",
                        botId
                    );
                    return root.State.GetLegalMoves().First();
                }
                else
                {
                    AlgoLog.Error(
                        "BotId {BotId}: No children, no untried actions, and no legal moves from root state. Returning a default 'Up' move.",
                        botId
                    );
                    return Move.Up;
                }
            }

            var bestChild = root.Children.OrderByDescending(c => c.Visits).FirstOrDefault();
            AlgoLog.Debug(
                "FindBestMove for BotId {BotId} determined best child with move: {Move}, Visits: {Visits}, Wins: {Wins}",
                botId,
                bestChild?.Move,
                bestChild?.Visits,
                bestChild?.Wins
            );

            if (bestChild != null && bestChild.Move.HasValue)
            {
                return bestChild.Move.Value;
            }
            var legalRootMoves = root.State.GetLegalMoves();
            if (legalRootMoves.Any())
            {
                AlgoLog.Information(
                    "BotId {BotId}: No best child from MCTS, falling back to first legal move from root: {Move}",
                    botId,
                    legalRootMoves.First()
                );
                return legalRootMoves.First();
            }
            AlgoLog.Error(
                "BotId {BotId}: No best child AND no legal moves from root state. Falling back to Move.Up.",
                botId
            );
            return Move.Up;
        }
    }
}
