using System.Diagnostics;
using MCTSo4.Models;
using Serilog;

namespace MCTSo4.Algorithms.MCTS;

/// <summary>
/// Parallel implementation of Monte Carlo Tree Search.
/// </summary>
public static class ParallelMctsAlgorithm
{
    private static readonly Random Rnd = new Random();
    private static readonly ILogger AlgoLog = Log.ForContext(typeof(ParallelMctsAlgorithm));

    // Maximum time allowed for MCTS iterations in milliseconds
    private const int MaxIterationTimeMs = 130;

    // Percentage of time budget to reserve for post-iteration overhead
    private const double OverheadTimePercentage = 0.65;

    // Thread pool size calculation constants
    private const int MinThreads = 1;
    private const int MaxThreads = 2;

    // Epsilon value for epsilon-greedy simulation
    private const double SimulationEpsilon = 0.7;

    /// <summary>
    /// Find the best move using parallel MCTS algorithm
    /// </summary>
    public static Move FindBestMove(
        MCTSGameState rootMctsState,
        Guid botId,
        BotParameters parameters,
        Stopwatch stopwatch
    )
    {
        if (botId == Guid.Empty)
        {
            AlgoLog.Error("FindBestMove called with Guid.Empty BotId. Returning a random move.");
            var legalMoves = rootMctsState.GetLegalMoves(botId);
            return legalMoves.Any() ? legalMoves[Rnd.Next(legalMoves.Count)] : Move.Up;
        }

        // Get time budget from parameters or use default
        int timeLimit =
            parameters.MaxTimePerMoveMs > 0 ? parameters.MaxTimePerMoveMs : MaxIterationTimeMs;

        // Reserve part of time budget for post-processing
        int iterationTimeLimit = (int)(timeLimit * (1.0 - OverheadTimePercentage));

        // Determine optimal thread count based on available cores
        int threadCount = CalculateOptimalThreadCount(parameters);

        AlgoLog.Debug(
            "Parallel MCTS starting for BotId: {BotId}, Threads: {ThreadCount}, TimeLimit: {TimeLimit}ms",
            botId,
            threadCount,
            timeLimit
        );

        // Initialize the shared root node
        var root = new ThreadSafeNode(
            rootMctsState.Clone(),
            null,
            null,
            parameters.ExplorationConstant,
            botId,
            parameters
        );

        // Force initial expansion of all root actions for better exploration
        ExpandAllRootActions(root, botId);

        // Set up cancellation for time limit
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        // Schedule cancellation at time limit
        cts.CancelAfter(iterationTimeLimit);

        // Start the parallel MCTS work
        var tasks = new Task[threadCount];
        var iterationCounters = new int[threadCount];
        var maxDepthTrackers = new int[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            int threadIndex = i; // Capture for lambda
            tasks[i] = Task.Run(
                () =>
                {
                    // Each thread gets its own random instance for thread safety
                    Random threadRandom = new Random(Guid.NewGuid().GetHashCode());

                    int iterations = 0;
                    int maxSelectionDepth = 0;

                    try
                    {
                        while (
                            !cancellationToken.IsCancellationRequested
                            && iterations < parameters.MctsIterations
                        )
                        {
                            iterations++;

                            try
                            {
                                // Run a complete MCTS iteration
                                int depthReached = MctsIteration(
                                    root,
                                    botId,
                                    parameters,
                                    threadRandom
                                );
                                maxSelectionDepth = Math.Max(maxSelectionDepth, depthReached);
                            }
                            catch (Exception ex)
                            {
                                // Log but continue with other iterations - don't crash the entire search
                                AlgoLog.Warning(
                                    ex,
                                    "Error in MCTS iteration in thread {ThreadIndex}, continuing with next iteration",
                                    threadIndex
                                );
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when time limit is reached
                    }
                    catch (Exception ex)
                    {
                        AlgoLog.Error(ex, "Error in MCTS thread {ThreadIndex}", threadIndex);
                    }

                    // Store statistics for reporting
                    iterationCounters[threadIndex] = iterations;
                    maxDepthTrackers[threadIndex] = maxSelectionDepth;
                },
                cancellationToken
            );
        }

        try
        {
            // Wait for all tasks to complete
            Task.WaitAll(tasks, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when time limit is reached
            AlgoLog.Information("MCTS search terminated due to time limit");
        }
        catch (AggregateException ae)
        {
            foreach (var ex in ae.InnerExceptions)
            {
                if (!(ex is OperationCanceledException))
                {
                    AlgoLog.Error(ex, "Error in MCTS parallel execution");
                }
            }
        }

        // Log statistics
        int totalIterations = iterationCounters.Sum();
        int maxDepthReached = maxDepthTrackers.Max();

        AlgoLog.Information(
            "Parallel MCTS Stats - BotId {BotId}: Threads: {ThreadCount}, Iterations: {TotalIterations}, Max Selection Depth: {MaxDepth}, Search Time: {SearchTime}ms",
            botId,
            threadCount,
            totalIterations,
            maxDepthReached,
            stopwatch.ElapsedMilliseconds
        );

        // Process results and select best move
        return SelectBestMove(root, botId);
    }

    /// <summary>
    /// Calculate optimal thread count based on system and parameters
    /// </summary>
    private static int CalculateOptimalThreadCount(BotParameters parameters)
    {
        // Use Environment.ProcessorCount - 1 to leave one core for the main thread
        int availableCores = Math.Max(1, Environment.ProcessorCount - 1);

        // Limit thread count based on min/max constants
        return Math.Min(MaxThreads, Math.Max(MinThreads, availableCores));
    }

    /// <summary>
    /// Force expansion of all legal moves at the root for balanced exploration
    /// </summary>
    private static void ExpandAllRootActions(ThreadSafeNode root, Guid botId)
    {
        // Use a temporary list to avoid thread safety issues during initial expansion
        var legalMoves = root.State.GetLegalMoves(botId);

        AlgoLog.Debug(
            "Expanding all {Count} root actions for initial exploration",
            legalMoves.Count
        );

        foreach (var move in legalMoves)
        {
            var nextState = root.State.Apply(move, botId);
            var child = new ThreadSafeNode(
                nextState,
                root,
                move,
                root.UctConstant,
                botId,
                root.Parameters
            );
            root.AddChild(child);
        }
    }

    /// <summary>
    /// Perform a single MCTS iteration with the given thread-local random generator
    /// </summary>
    private static int MctsIteration(
        ThreadSafeNode root,
        Guid botId,
        BotParameters parameters,
        Random random
    )
    {
        // 1. Selection phase
        var selectedNode = root;
        var pathToLeaf = new List<ThreadSafeNode> { selectedNode };
        int selectionDepth = 0;

        // Continue selection until we reach a node that isn't fully expanded or is terminal
        while (!selectedNode.IsTerminal(0, int.MaxValue) && selectedNode.IsFullyExpanded)
        {
            // Apply virtual loss to discourage other threads from selecting this path
            selectedNode.AddVirtualLoss();

            // Check for children before trying to select best child
            if (!selectedNode.Children.Any())
            {
                // If we somehow have a node that is marked as fully expanded but has no children,
                // break out of the selection loop and continue with expansion or simulation
                AlgoLog.Warning(
                    "Node marked as fully expanded but has no children - breaking selection loop"
                );
                break;
            }

            // Select best child according to UCT
            selectedNode = selectedNode.BestChild();
            pathToLeaf.Add(selectedNode);
            selectionDepth++;
        }

        // 2. Expansion phase
        if (!selectedNode.IsTerminal(0, int.MaxValue) && selectedNode.HasUntriedActions)
        {
            if (selectedNode.TryGetUntriedAction(out var actionToExpand))
            {
                var nextState = selectedNode.State.Apply(actionToExpand, botId);
                var child = new ThreadSafeNode(
                    nextState,
                    selectedNode,
                    actionToExpand,
                    parameters.ExplorationConstant,
                    botId,
                    parameters
                );

                // Apply virtual loss to new node as well
                child.AddVirtualLoss();

                // Add child to parent
                selectedNode.AddChild(child);

                // Continue with simulation from the new node
                selectedNode = child;
                pathToLeaf.Add(selectedNode);
                selectionDepth++;
            }
        }

        // 3. Simulation phase
        SimulationResult result = SimulateWithHeuristics(selectedNode, botId, parameters, random);

        // 4. Backpropagation phase with RAVE updates
        BackpropagateWithRave(pathToLeaf, result, parameters);

        return selectionDepth;
    }

    /// <summary>
    /// Data structure to capture both reward and move sequence from simulation
    /// </summary>
    private class SimulationResult
    {
        public double Reward { get; set; }
        public List<Move> MoveSequence { get; set; } = new List<Move>();
    }

    /// <summary>
    /// Run a simulation using epsilon-greedy and heuristic guidance
    /// </summary>
    private static SimulationResult SimulateWithHeuristics(
        ThreadSafeNode node,
        Guid botId,
        BotParameters parameters,
        Random random
    )
    {
        var result = new SimulationResult();

        // Use FastGameState for efficient simulation
        FastGameState fastState = new FastGameState();
        fastState.InitializeFromMCTSGameState(node.State, botId);

        // Track metrics for simulation
        int pelletsCollected = 0;
        int zookeeperAvoidances = 0;

        // Run simulation for specified depth
        for (int step = 0; step < parameters.MctsDepth; step++)
        {
            // Check if simulation should terminate
            if (fastState.IsTerminalFast(step, parameters.MctsDepth, parameters))
                break;

            // Get legal moves
            var legalMoves = fastState.GetLegalMovesFast();
            if (!legalMoves.Any())
                break;

            Move selectedMove;

            // Epsilon-greedy approach: sometimes random, sometimes heuristic
            if (random.NextDouble() < SimulationEpsilon)
            {
                // Use heuristic guidance: evaluate all moves and select proportionally to their scores
                var moveScores = new Dictionary<Move, double>();
                double totalScore = 0;

                // Score each legal move
                foreach (var move in legalMoves)
                {
                    var nextState = fastState.ApplyFast(move);
                    double moveScore = EvaluateMoveHeuristically(
                        fastState,
                        nextState,
                        move,
                        parameters
                    );

                    // Ensure scores are positive for selection probability
                    moveScore = Math.Max(0.1, moveScore + 1000);
                    moveScores[move] = moveScore;
                    totalScore += moveScore;
                }

                // Select a move based on proportional probabilities
                double r = random.NextDouble() * totalScore;
                double cumulativeScore = 0;

                selectedMove = legalMoves[0]; // Default to first move
                foreach (var moveScore in moveScores)
                {
                    cumulativeScore += moveScore.Value;
                    if (cumulativeScore >= r)
                    {
                        selectedMove = moveScore.Key;
                        break;
                    }
                }
            }
            else
            {
                // Pure random selection
                selectedMove = legalMoves[random.Next(legalMoves.Count)];
            }

            // Record move for RAVE
            result.MoveSequence.Add(selectedMove);

            // Track state before move to detect pellet collection and zookeeper avoidance
            int pelletsBeforeMove = fastState.PelletsCollected;
            int avoidancesBeforeMove = fastState.ZookeeperAvoidances;

            // Apply selected move
            fastState = fastState.ApplyFast(selectedMove);

            // Update metrics
            pelletsCollected += (fastState.PelletsCollected - pelletsBeforeMove);
            zookeeperAvoidances += (fastState.ZookeeperAvoidances - avoidancesBeforeMove);
        }

        // Evaluate final state using heuristic function
        double evaluationScore = fastState.EvaluateFast(parameters);

        // Apply cubic reward transformation to create stronger differentiation
        // between good and bad moves
        result.Reward = Math.Sign(evaluationScore) * Math.Pow(Math.Abs(evaluationScore), 3);

        return result;
    }

    /// <summary>
    /// Simple heuristic to evaluate a move during simulation
    /// </summary>
    private static double EvaluateMoveHeuristically(
        FastGameState currentState,
        FastGameState nextState,
        Move move,
        BotParameters parameters
    )
    {
        // Calculate the difference in evaluation between current and next state
        double currentEval = currentState.EvaluateFast(parameters);
        double nextEval = nextState.EvaluateFast(parameters);

        // Reward improvement in evaluation
        return nextEval - currentEval;
    }

    /// <summary>
    /// Backpropagate results and update RAVE statistics
    /// </summary>
    private static void BackpropagateWithRave(
        List<ThreadSafeNode> pathToLeaf,
        SimulationResult result,
        BotParameters parameters
    )
    {
        // Determine how many moves to use for RAVE updates (limited by parameter)
        int raveDepth = Math.Min(result.MoveSequence.Count, parameters.RaveMaxDepth);

        // Standard backpropagation - update statistics for all nodes in the path
        foreach (var node in pathToLeaf)
        {
            // Update statistics
            node.UpdateStats(result.Reward);

            // Remove virtual loss applied during selection
            node.RemoveVirtualLoss();
        }

        // RAVE updates - for each node, update RAVE statistics for all moves in simulation
        if (parameters.RaveWeight > 0)
        {
            // Only update RAVE up to limited depth for efficiency
            for (int i = 0; i < Math.Min(pathToLeaf.Count, parameters.RaveMaxDepth); i++)
            {
                var node = pathToLeaf[i];

                // Update RAVE statistics for all moves in the simulation
                for (int j = 0; j < raveDepth; j++)
                {
                    if (j < result.MoveSequence.Count)
                    {
                        node.UpdateRaveStats(result.MoveSequence[j], result.Reward);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Select the best move based on visit counts and scores
    /// </summary>
    private static Move SelectBestMove(ThreadSafeNode root, Guid botId)
    {
        // Get all children of the root
        var rootChildren = root.Children.ToList();

        if (!rootChildren.Any())
        {
            AlgoLog.Warning("No children found at root node. Returning a random legal move.");
            var legalMoves = root.State.GetLegalMoves(botId);
            return legalMoves.Any() ? legalMoves[Rnd.Next(legalMoves.Count)] : Move.Up;
        }

        // Log stats for each move
        foreach (var child in rootChildren)
        {
            AlgoLog.Information(
                "Move Stats - BotId {BotId}: Move: {Move}, Visits: {Visits}, Win Rate: {WinRate:F2}, Raw Score: {RawScore:F4}",
                botId,
                child.Move,
                child.Visits,
                child.Visits > 0 ? child.Wins / child.Visits : 0,
                child.Visits > 0 ? child.Wins / child.Visits : 0
            );
        }

        // Find the most visited node
        var mostVisitedNode = rootChildren.OrderByDescending(c => c.Visits).First();

        // Find the node with highest win rate
        var highestWinRateNode = rootChildren
            .Where(c => c.Visits > 0) // Ensure we have valid statistics
            .OrderByDescending(c => c.Wins / c.Visits)
            .First();

        AlgoLog.Information(
            "Selected move based on win rate: {WinRateMove} ({WinRate:F2}) over most visited: {MostVisitedMove} ({MostVisitedWinRate:F2})",
            highestWinRateNode.Move,
            highestWinRateNode.Visits > 0 ? highestWinRateNode.Wins / highestWinRateNode.Visits : 0,
            mostVisitedNode.Move,
            mostVisitedNode.Visits > 0 ? mostVisitedNode.Wins / mostVisitedNode.Visits : 0
        );

        // Robust policy: select move with highest score (win rate)
        var selectedNode = highestWinRateNode;

        AlgoLog.Information(
            "Selected final move with highest score: {Move}, Score: {Score:F2}, Visits: {Visits}",
            selectedNode.Move,
            selectedNode.Visits > 0 ? selectedNode.Wins / selectedNode.Visits : 0,
            selectedNode.Visits
        );

        return selectedNode.Move ?? Move.Up; // Fallback to Up if null
    }
}
