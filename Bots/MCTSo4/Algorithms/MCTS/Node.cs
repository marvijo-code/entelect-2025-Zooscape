using MCTSo4.Models;

namespace MCTSo4.Algorithms.MCTS;

public class MctsNode
{
    public MCTSGameState State { get; }
    public MctsNode? Parent { get; }
    public Move? Move { get; }
    public List<MctsNode> Children { get; }
    public List<Move> UntriedActions { get; }
    public double Wins { get; set; }
    public int Visits { get; set; }
    public double UctConstant { get; }
    public Guid BotId { get; }
    public BotParameters Parameters { get; }

    public MctsNode(
        MCTSGameState state,
        MctsNode? parent,
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
        Children = new List<MctsNode>();
        UntriedActions = state.GetLegalMoves();
        Wins = 0;
        Visits = 0;
    }

    public bool IsFullyExpanded => UntriedActions.Count == 0;

    public bool IsTerminal(int currentTickInSim, int maxSimDepth) =>
        State.IsTerminal(BotId, Parameters, currentTickInSim, maxSimDepth);

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

        // Calculate UCT using raw win rate - no normalization needed anymore
        double winRate = Wins / Visits;

        // Our raw scores can be very large now (50-150+) so we need to scale down
        // the exploration term to make it proportional
        double explorationBalance = Math.Min(Math.Abs(winRate) * 0.1, 1.0);
        double explorationTerm = UctConstant * Math.Sqrt(Math.Log(Parent.Visits) / Visits);

        // Scale down exploration term based on the magnitude of the rewards
        return winRate + explorationTerm * explorationBalance;
    }

    public MctsNode BestChild()
    {
        if (Children == null || !Children.Any())
        {
            throw new InvalidOperationException("BestChild called on a node with no children.");
        }
        return Children.OrderByDescending(c => c.UctValue()).First();
    }
}
