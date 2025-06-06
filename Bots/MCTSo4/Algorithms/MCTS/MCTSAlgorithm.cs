using System.Diagnostics;
using MCTSo4.Models;
using Serilog;

namespace MCTSo4.Algorithms.MCTS;

/// <summary>
/// Monte Carlo Tree Search implementation.
/// </summary>
public static class MctsAlgorithm
{
    private static readonly Random Rnd = new Random();
    private static readonly ILogger AlgoLog = Log.ForContext(typeof(MctsAlgorithm));

    // Maximum time allowed for MCTS iterations in milliseconds
    private const int MaxIterationTimeMs = 130; // Limited to 130ms max

    // Percentage of time budget to reserve for post-iteration overhead (selecting best move, logging, etc.)
    private const double OverheadTimePercentage = 0.65; // 65% of budget reserved for overhead

    public static Move FindBestMove(
        MCTSGameState rootMctsState,
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
            var legalMoves = rootMctsState.GetLegalMoves();
            return legalMoves.Any() ? legalMoves[Rnd.Next(legalMoves.Count)] : Move.Up; // Default to Up if no legal moves somehow
        }

        // Get the time budget from parameters or use the default
        int timeLimit =
            parameters.MaxTimePerMoveMs > 0 ? parameters.MaxTimePerMoveMs : MaxIterationTimeMs;

        // Reserve part of the time budget for post-iteration processing
        int iterationTimeLimit = (int)(timeLimit * (1.0 - OverheadTimePercentage));

        AlgoLog.Debug(
            "FindBestMove called for BotId: {BotId}. MctsIterations: {Iterations}, MctsDepth: {Depth}, Exploration: {Exploration}, TimeLimit: {TimeLimit}ms, IterationTimeLimit: {IterationLimit}ms",
            botId,
            parameters.MctsIterations,
            parameters.MctsDepth,
            parameters.ExplorationConstant,
            timeLimit,
            iterationTimeLimit
        );
        var root = new MctsNode(
            rootMctsState.Clone(),
            null,
            null,
            parameters.ExplorationConstant,
            botId,
            parameters
        );

        int maxSelectionDepthReached = 0;
        int totalIterations = 0;

        for (int i = 0; i < parameters.MctsIterations; i++)
        {
            totalIterations++;
            if (stopwatch.ElapsedMilliseconds >= iterationTimeLimit)
            {
                AlgoLog.Warning(
                    "MCTS terminating iteration {Iteration} for BotId {BotId} early due to time limit ({ElapsedMs}ms >= {TimeLimit}ms)",
                    i + 1,
                    botId,
                    stopwatch.ElapsedMilliseconds,
                    iterationTimeLimit
                );
                break;
            }
            AlgoLog.Verbose(
                "MCTS Iteration {Iteration}/{TotalIterations} for BotId {BotId} starting...",
                i + 1,
                parameters.MctsIterations,
                botId
            );

            var nodeToSimulateFrom = root;
            int selectionTreeDepth = 0;

            // Ensure initial root level exploration by forcing initial moves to be more balanced
            if (i < 40 && root.UntriedActions.Any())
            {
                // For the first few iterations, force expansion of untried actions
                // This ensures we explore all moves before getting too deep in any one branch
                AlgoLog.Debug(
                    "Forcing exploration of untried action for iteration {Iteration}",
                    i + 1
                );
                var actionIndex = Rnd.Next(root.UntriedActions.Count);
                var actionToExpand = root.UntriedActions[actionIndex];
                root.UntriedActions.RemoveAt(actionIndex);
                var nextMctsState = root.State.Apply(actionToExpand, botId);
                var child = new MctsNode(
                    nextMctsState,
                    root,
                    actionToExpand,
                    parameters.ExplorationConstant,
                    botId,
                    parameters
                );
                root.Children.Add(child);
                nodeToSimulateFrom = child;
            }
            else
            {
                // Standard selection phase
                AlgoLog.Verbose("Starting Selection phase for BotId {BotId}...", botId);
                while (
                    !nodeToSimulateFrom.IsTerminal(0, int.MaxValue)
                    && nodeToSimulateFrom.IsFullyExpanded
                )
                {
                    nodeToSimulateFrom = nodeToSimulateFrom.BestChild();
                    AlgoLog.Verbose(
                        "Selected node: {NodeAction}, Visits: {Visits}, Wins: {Wins} for BotId {BotId}",
                        nodeToSimulateFrom.Move,
                        nodeToSimulateFrom.Visits,
                        nodeToSimulateFrom.Wins,
                        botId
                    );
                    selectionTreeDepth++;
                }
            }

            // Track maximum selection depth
            maxSelectionDepthReached = Math.Max(maxSelectionDepthReached, selectionTreeDepth);

            AlgoLog.Verbose(
                "Selection phase complete for BotId {BotId}. Selected node leads with action: {Action}",
                botId,
                nodeToSimulateFrom.Move
            );

            AlgoLog.Verbose("Starting Expansion phase for BotId {BotId}...", botId);
            MctsNode expandedNodeForRollout = nodeToSimulateFrom;
            if (
                !nodeToSimulateFrom.IsTerminal(0, int.MaxValue)
                && nodeToSimulateFrom.UntriedActions.Any()
            )
            {
                AlgoLog.Debug("Expansion criteria met for BotId {BotId}. Expanding node.", botId);
                var actionIndex = Rnd.Next(nodeToSimulateFrom.UntriedActions.Count);
                var actionToExpand = nodeToSimulateFrom.UntriedActions[actionIndex];
                nodeToSimulateFrom.UntriedActions.RemoveAt(actionIndex);
                var nextMctsState = nodeToSimulateFrom.State.Apply(actionToExpand, botId);
                var child = new MctsNode(
                    nextMctsState,
                    nodeToSimulateFrom,
                    actionToExpand,
                    parameters.ExplorationConstant,
                    botId,
                    parameters
                );
                nodeToSimulateFrom.Children.Add(child);
                expandedNodeForRollout = child;
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
                    nodeToSimulateFrom.Move
                );
            }
            AlgoLog.Verbose("Expansion phase complete for BotId {BotId}.", botId);

            AlgoLog.Verbose(
                "Starting Simulation (Rollout) with FastGameState from node with action: {Action} for BotId {BotId}...",
                expandedNodeForRollout.Move,
                botId
            );
            FastGameState fastRolloutState = new FastGameState();
            fastRolloutState.InitializeFromMCTSGameState(expandedNodeForRollout.State, botId);

            int rolloutStepCount = 0;
            for (rolloutStepCount = 0; rolloutStepCount < parameters.MctsDepth; rolloutStepCount++)
            {
                if (
                    fastRolloutState.IsTerminalFast(
                        rolloutStepCount,
                        parameters.MctsDepth,
                        parameters
                    )
                )
                {
                    AlgoLog.Verbose(
                        "FastRollout for BotId {BotId} reached terminal at step {Step}",
                        botId,
                        rolloutStepCount
                    );
                    break;
                }

                var legalMoves = fastRolloutState.GetLegalMovesFast();
                if (!legalMoves.Any())
                {
                    AlgoLog.Warning(
                        "FastRollout for BotId {BotId}: No valid moves. Breaking.",
                        botId
                    );
                    break;
                }

                var selectableMoves = legalMoves.ToList();
                if (!selectableMoves.Any())
                {
                    AlgoLog.Warning(
                        "FastRollout for BotId {BotId}: No selectable moves found. Breaking.",
                        botId
                    );
                    break;
                }

                var randomAction = selectableMoves[Rnd.Next(selectableMoves.Count)];
                fastRolloutState = fastRolloutState.ApplyFast(randomAction);
            }

            if (
                rolloutStepCount == parameters.MctsDepth
                && !fastRolloutState.IsTerminalFast(
                    rolloutStepCount,
                    parameters.MctsDepth,
                    parameters
                )
            )
            {
                AlgoLog.Verbose(
                    "FastRollout for BotId {BotId} reached max depth {MaxDepth}.",
                    botId,
                    parameters.MctsDepth
                );
            }

            var baseReward = fastRolloutState.EvaluateFast(parameters);

            // This no longer needs the Math.Sign adjustment since we're using raw values
            // We still use Math.Pow but with a smaller exponent to prevent extreme values
            var reward = Math.Pow(Math.Abs(baseReward), 1.5) * Math.Sign(baseReward);

            AlgoLog.Debug(
                "FastSimulation (Rollout) for BotId {BotId} complete. Steps: {Steps}, Base Reward: {BaseReward}, Modified Reward: {Reward}, Terminal: {IsTerminalNow}",
                botId,
                rolloutStepCount,
                baseReward,
                reward,
                fastRolloutState.IsTerminalFast(rolloutStepCount, parameters.MctsDepth, parameters)
            );

            AlgoLog.Verbose("Starting Backpropagation phase for BotId {BotId}...", botId);
            var tempNode = expandedNodeForRollout;
            while (tempNode != null)
            {
                tempNode.Visits++;
                tempNode.Wins += reward;

                // Adjust the clamping values to match our new scale
                if (tempNode.Wins > 10000 || tempNode.Wins < -10000)
                {
                    tempNode.Wins = Math.Sign(tempNode.Wins) * 10000;
                    AlgoLog.Debug(
                        "Clamped extreme win value for node with move {Move}",
                        tempNode.Move
                    );
                }

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
                "MCTS for BotId {BotId}: Root node has no children. Untried: {Count}",
                botId,
                root.UntriedActions.Count
            );
            if (root.UntriedActions.Any())
            {
                AlgoLog.Information("BotId {BotId}: Fallback to first untried from root.", botId);
                return root.UntriedActions.First();
            }
            else if (rootMctsState.GetLegalMoves().Any())
            {
                AlgoLog.Information(
                    "BotId {BotId}: Fallback to first legal from root state.",
                    botId
                );
                return rootMctsState.GetLegalMoves().First();
            }
            else
            {
                AlgoLog.Error("BotId {BotId}: No children, untried, or legal. Default Up.", botId);
                return Move.Up;
            }
        }

        // Log information about search depth and move visits
        AlgoLog.Information(
            "MCTS Stats - BotId {BotId}: Iterations: {Iterations}, Max Selection Depth: {MaxDepth}, Search Time: {ElapsedMs}ms",
            botId,
            totalIterations,
            maxSelectionDepthReached,
            stopwatch.ElapsedMilliseconds
        );

        // Calculate normalized win rates for all children
        Dictionary<Move, double> moveScores = new Dictionary<Move, double>();
        foreach (var child in root.Children)
        {
            // Use raw win/visit ratio without normalization to show actual differences
            double rawWinRate = child.Visits > 0 ? child.Wins / child.Visits : 0;

            // Don't apply the (x + 1.0) * 50.0 formula - just display the raw value as percentage
            double winRatePercentage = rawWinRate;

            if (child.Move.HasValue)
            {
                moveScores[child.Move.Value] = winRatePercentage;
            }

            AlgoLog.Information(
                "Move Stats - BotId {BotId}: Move: {Move}, Visits: {Visits}, Win Rate: {WinRate:F2}, Raw Score: {RawScore:F4}",
                botId,
                child.Move,
                child.Visits,
                winRatePercentage,
                rawWinRate
            );
        }

        // First find the most visited move as a baseline
        var mostVisitedChild = root.Children.OrderByDescending(c => c.Visits).FirstOrDefault();

        // Apply a minimum visit threshold before considering a move's win rate
        var minVisitsThreshold = Math.Max(5, root.Children.Max(c => c.Visits) / 10);

        // Then find the move with the best win rate among moves with sufficient visits
        var bestWinRateChild = root
            .Children.Where(c => c.Visits >= minVisitsThreshold)
            .OrderByDescending(c => c.Visits > 0 ? c.Wins / c.Visits : double.MinValue)
            .FirstOrDefault();

        var bestChild = bestWinRateChild ?? mostVisitedChild;

        // Log decision rationale
        if (
            bestWinRateChild != null
            && mostVisitedChild != null
            && bestWinRateChild != mostVisitedChild
        )
        {
            double bestRawWinRate = bestWinRateChild.Wins / bestWinRateChild.Visits;
            double mostVisitedRawWinRate = mostVisitedChild.Wins / mostVisitedChild.Visits;

            AlgoLog.Information(
                "Selected move based on win rate: {Move} ({WinRate:F2}) over most visited: {VisitedMove} ({VisitedWinRate:F2})",
                bestWinRateChild.Move,
                bestRawWinRate,
                mostVisitedChild.Move,
                mostVisitedRawWinRate
            );
        }

        AlgoLog.Debug(
            "FindBestMove for BotId {BotId} determined best child: {Move}, Visits: {V}, Wins: {W}",
            botId,
            bestChild?.Move,
            bestChild?.Visits,
            bestChild?.Wins
        );

        // Find the final best move based strictly on the highest raw win rate among well-visited nodes
        if (root.Children.Any(c => c.Visits >= minVisitsThreshold))
        {
            // Get the move with the absolute highest score (without any other considerations)
            var highestScoreMove = root
                .Children.Where(c => c.Visits >= minVisitsThreshold)
                .OrderByDescending(c => c.Visits > 0 ? c.Wins / c.Visits : double.MinValue)
                .First();

            AlgoLog.Information(
                "Selected final move with highest score: {Move}, Score: {Score:F2}, Visits: {Visits}",
                highestScoreMove.Move,
                highestScoreMove.Wins / highestScoreMove.Visits,
                highestScoreMove.Visits
            );

            if (highestScoreMove.Move.HasValue)
            {
                return highestScoreMove.Move.Value;
            }
        }

        // Fallback to the original algorithm's choice if something went wrong
        if (bestChild != null && bestChild.Move.HasValue)
        {
            return bestChild.Move.Value;
        }
        var legalRootMoves = rootMctsState.GetLegalMoves();
        if (legalRootMoves.Any())
        {
            AlgoLog.Information(
                "BotId {BotId}: No best child with move, fallback to root legal: {Move}",
                botId,
                legalRootMoves.First()
            );
            return legalRootMoves.First();
        }
        AlgoLog.Error("BotId {BotId}: No best child, no root legal. Default Up.", botId);
        return Move.Up;
    }
}
