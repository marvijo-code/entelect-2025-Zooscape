using System.Collections.Concurrent;
using MCTSo4.Models;

namespace MCTSo4.Algorithms.MCTS;

/// <summary>
/// Thread-safe version of MCTS node for concurrent tree search
/// </summary>
public class ThreadSafeNode
{
    public MCTSGameState State { get; }
    public ThreadSafeNode? Parent { get; }
    public Move? Move { get; }

    // Thread-safe collections for parallel access
    private readonly ConcurrentBag<ThreadSafeNode> _children;
    public IReadOnlyCollection<ThreadSafeNode> Children => _children;

    // Use concurrent queue for untried actions to safely distribute work across threads
    private readonly ConcurrentQueue<Move> _untriedActions;
    public bool HasUntriedActions => !_untriedActions.IsEmpty;

    // Keep track of all legal moves for progressive widening
    private readonly List<Move> _allLegalMoves;

    // Atomic counters for thread-safe updates
    private long _wins;
    private int _visits;
    private int _virtualLoss;

    // RAVE (Rapid Action Value Estimation) counters
    private readonly ConcurrentDictionary<Move, long> _raveWins;
    private readonly ConcurrentDictionary<Move, int> _raveVisits;

    public double Wins => Interlocked.Read(ref _wins) / 1000.0;
    public int Visits => Interlocked.Add(ref _visits, 0);
    public int VirtualLoss => Interlocked.Add(ref _virtualLoss, 0);

    public double UctConstant { get; }
    public Guid BotId { get; }
    public BotParameters Parameters { get; }

    public ThreadSafeNode(
        MCTSGameState state,
        ThreadSafeNode? parent,
        Move? move,
        double uctConstant,
        Guid botId,
        BotParameters parameters
    )
    {
        State = state;
        Parent = parent;
        Move = move;
        UctConstant = uctConstant;
        BotId = botId;
        Parameters = parameters;

        _children = new ConcurrentBag<ThreadSafeNode>();

        // Store all legal moves for progressive widening
        _allLegalMoves = state.GetLegalMoves();

        // Initially make all moves available if progressive widening is not used
        _untriedActions = new ConcurrentQueue<Move>(_allLegalMoves);

        // Initialize RAVE tracking - track all possible moves
        _raveWins = new ConcurrentDictionary<Move, long>();
        _raveVisits = new ConcurrentDictionary<Move, int>();

        foreach (var action in Enum.GetValues<Move>())
        {
            _raveWins[action] = 0;
            _raveVisits[action] = 0;
        }

        _wins = 0;
        _visits = 0;
        _virtualLoss = 0;
    }

    /// <summary>
    /// Determines if this node is fully expanded considering progressive widening
    /// </summary>
    public bool IsFullyExpanded
    {
        get
        {
            // A node with no children can't be fully expanded, regardless of untried actions
            // This prevents the BestChild() error when a node incorrectly reports being fully expanded
            if (_children.Count == 0 && _allLegalMoves.Count > 0)
                return false;

            // If using progressive widening, check if we've expanded enough children
            if (Parameters.ProgressiveWideningBase > 0)
            {
                int currentChildCount = _children.Count;
                int allowedChildCount = CalculateProgressiveWidening();

                // Not fully expanded if we haven't reached the allowed child count yet
                return currentChildCount >= allowedChildCount || _untriedActions.IsEmpty;
            }

            // Without progressive widening, fully expanded when all actions are tried
            return _untriedActions.IsEmpty;
        }
    }

    /// <summary>
    /// Calculate the allowed number of children based on progressive widening formula
    /// </summary>
    private int CalculateProgressiveWidening()
    {
        if (Visits == 0 || Parameters.ProgressiveWideningBase <= 0)
            return _allLegalMoves.Count; // Allow all moves if no visits or progressive widening is disabled

        // Progressive widening formula: k * N^α
        double allowedChildrenDouble =
            Parameters.ProgressiveWideningBase
            * Math.Pow(Visits, Parameters.ProgressiveWideningExponent);

        // Ensure at least 1 and at most the number of legal moves
        int allowedChildren = Math.Max(1, (int)Math.Ceiling(allowedChildrenDouble));
        return Math.Min(allowedChildren, _allLegalMoves.Count);
    }

    public bool IsTerminal(int currentTickInSim, int maxSimDepth) =>
        State.IsTerminal(BotId, Parameters, currentTickInSim, maxSimDepth);

    /// <summary>
    /// Get the UCT value for the node, enhanced with RAVE
    /// </summary>
    public double UctValue()
    {
        // If no visits, make it highly explorable
        if (Visits == 0)
            return double.MaxValue;

        // If parent doesn't exist or has no visits, just return raw win rate
        if (Parent == null || Parent.Visits == 0)
        {
            return (Visits > 0) ? Wins / Visits : double.MaxValue;
        }

        // Calculate standard UCT using raw win rate
        double winRate = Wins / Visits;

        // Account for virtual loss in exploration calculation
        int effectiveVisits = Visits + VirtualLoss;

        // Scale exploration term based on reward magnitude
        double explorationBalance = Math.Min(Math.Abs(winRate) * 0.1, 1.0);
        double explorationTerm = UctConstant * Math.Sqrt(Math.Log(Parent.Visits) / effectiveVisits);

        // Base UCT score
        double uctScore = winRate + explorationTerm * explorationBalance;

        // Only apply RAVE if enabled and we have a valid move
        if (Parameters.RaveWeight > 0 && Move.HasValue)
        {
            // Get RAVE statistics for this move from parent
            if (Parent._raveVisits.TryGetValue(Move.Value, out int raveVisits) && raveVisits > 0)
            {
                if (Parent._raveWins.TryGetValue(Move.Value, out long raveWinsLong))
                {
                    double raveWins = raveWinsLong / 1000.0; // Convert from long storage
                    double raveWinRate = raveWins / raveVisits;

                    // Calculate RAVE weight based on visit counts (decreases as visits increase)
                    // beta = k / (k + visits) where k is the equivalence parameter
                    double beta =
                        Parameters.RaveEquivalenceParameter
                        / (Parameters.RaveEquivalenceParameter + Visits);

                    // Combine UCT and RAVE scores using weighted average
                    return (1 - beta) * uctScore + beta * raveWinRate;
                }
            }
        }

        return uctScore;
    }

    /// <summary>
    /// Adds virtual loss to this node to discourage other threads from selecting it
    /// </summary>
    public void AddVirtualLoss()
    {
        // Add multiple virtual losses based on configuration
        Interlocked.Add(ref _virtualLoss, Parameters.VirtualLossCount);
    }

    /// <summary>
    /// Removes virtual loss after simulation is complete
    /// </summary>
    public void RemoveVirtualLoss()
    {
        // Remove the same number of virtual losses that were added
        Interlocked.Add(ref _virtualLoss, -Parameters.VirtualLossCount);
    }

    /// <summary>
    /// Atomically update wins and visits
    /// </summary>
    public void UpdateStats(double reward)
    {
        // Multiply by 1000 to store as long while preserving 3 decimal places
        Interlocked.Add(ref _wins, (long)(reward * 1000));
        Interlocked.Increment(ref _visits);
    }

    /// <summary>
    /// Update RAVE statistics for a move
    /// </summary>
    public void UpdateRaveStats(Move move, double reward)
    {
        if (Parameters.RaveWeight <= 0)
            return;

        // Convert reward to long format (x1000)
        long rewardLong = (long)(reward * 1000);

        // Atomic update of RAVE statistics
        _raveVisits.AddOrUpdate(
            move,
            1, // Initial visit count if key doesn't exist
            (_, visits) => visits + 1 // Increment existing count
        );

        _raveWins.AddOrUpdate(
            move,
            rewardLong, // Initial win value if key doesn't exist
            (_, wins) => wins + rewardLong // Add reward to existing wins
        );
    }

    /// <summary>
    /// Try to get an untried action using progressive widening if enabled
    /// </summary>
    public bool TryGetUntriedAction(out Move action)
    {
        // Check if we should add more children according to progressive widening
        if (Parameters.ProgressiveWideningBase > 0)
        {
            int currentChildCount = _children.Count;
            int allowedChildCount = CalculateProgressiveWidening();

            if (currentChildCount >= allowedChildCount)
            {
                // We've reached the limit for this node's visits
                action = default;
                return false;
            }
        }

        // First try to get an action from the queue
        if (_untriedActions.TryDequeue(out action))
            return true;

        // If queue is empty but we need more children due to increased visit count,
        // we could potentially re-add some actions here based on UCB scores

        return false;
    }

    /// <summary>
    /// Add a child node thread-safely
    /// </summary>
    public void AddChild(ThreadSafeNode child)
    {
        _children.Add(child);
    }

    /// <summary>
    /// Find the best child using UCT enhanced with RAVE
    /// </summary>
    public ThreadSafeNode BestChild()
    {
        if (!_children.Any())
        {
            throw new InvalidOperationException("BestChild called on a node with no children.");
        }

        // Since ConcurrentBag doesn't guarantee ordering, we need to iterate through all children
        return _children.OrderByDescending(c => c.UctValue()).First();
    }

    /// <summary>
    /// Find the most robust child (highest visit count) for final move selection
    /// </summary>
    public ThreadSafeNode MostRobustChild()
    {
        if (!_children.Any())
        {
            throw new InvalidOperationException(
                "MostRobustChild called on a node with no children."
            );
        }

        return _children.OrderByDescending(c => c.Visits).First();
    }
}
