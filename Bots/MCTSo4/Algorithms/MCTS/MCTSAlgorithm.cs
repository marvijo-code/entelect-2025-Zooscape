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
                AlgoLog.Debug(
                    "MCTS Iteration {Iteration}/{TotalIterations} for BotId {BotId} starting...",
                    i + 1,
                    parameters.MctsIterations,
                    botId
                );

                var node = root;
                int selectionDepth = 0;

                // Selection
                AlgoLog.Debug("Starting Selection phase for BotId {BotId}...", botId);
                while (
                    !node.IsTerminal(selectionDepth, parameters.MctsDepth) && node.IsFullyExpanded
                )
                {
                    node = node.BestChild();
                    AlgoLog.Verbose(
                        "Selected node: {NodeAction}, Visits: {Visits}, Wins: {Wins} for BotId {BotId}",
                        node.Move,
                        node.Visits,
                        node.Wins,
                        botId
                    );
                    selectionDepth++;
                }
                AlgoLog.Debug(
                    "Selection phase complete for BotId {BotId}. Selected node leads with action: {Action}",
                    botId,
                    node.Move
                );

                // Expansion
                AlgoLog.Debug("Starting Expansion phase for BotId {BotId}...", botId);
                if (
                    !node.IsTerminal(selectionDepth, parameters.MctsDepth)
                    && node.UntriedActions.Any()
                )
                {
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
                    AlgoLog.Debug(
                        "Expanded with action: {Action} for BotId {BotId}. New node created.",
                        actionToExpand,
                        botId
                    );
                }
                else
                {
                    AlgoLog.Debug(
                        "Expansion phase for BotId {BotId}: Node is terminal or no untried actions. Current node action: {Action}",
                        botId,
                        node.Move
                    );
                }
                AlgoLog.Debug("Expansion phase complete for BotId {BotId}.", botId);

                // Simulation (Rollout)
                AlgoLog.Debug(
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
                        AlgoLog.Debug(
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
                    AlgoLog.Verbose(
                        "Rollout step {Step}/{MaxDepth} for BotId {BotId}, iteration {Iteration}: Applied random action {Action}",
                        rolloutStepCount + 1,
                        parameters.MctsDepth,
                        botId,
                        i + 1,
                        randomAction
                    );
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
                    AlgoLog.Debug(
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

                // Backpropagation
                AlgoLog.Debug("Starting Backpropagation phase for BotId {BotId}...", botId);
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
                AlgoLog.Debug("Backpropagation phase complete for BotId {BotId}.", botId);
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
            // If no best child or best child has no move, try first legal move from root state
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
            // Ultimate fallback if no legal moves from root state either (should be rare)
            AlgoLog.Error(
                "BotId {BotId}: No best child AND no legal moves from root state. Falling back to Move.Up.",
                botId
            );
            return Move.Up;
        }
    }
}
