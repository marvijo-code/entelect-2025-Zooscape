using System;
using System.Collections.Generic;
using System.Linq;
using MCTSBot.Models;

namespace MCTSBot.Algorithms.MCTS;

public class Node
{
    public MCTSGameState GameState { get; }
    public Node? Parent { get; }
    public GameAction? Move { get; } // The move that led to this state

    public List<Node> Children { get; }
    private List<GameAction> untriedMoves;

    public int Visits { get; private set; }
    public double Wins { get; private set; } // Could be score or win count

    private static Random random = new Random();

    public Node(MCTSGameState gameState, Node? parent = null, GameAction? move = null)
    {
        GameState = gameState;
        Parent = parent;
        Move = move;
        Children = new List<Node>();
        untriedMoves = GameState.GetPossibleMoves();
        Visits = 0;
        Wins = 0;
    }

    public bool IsFullyExpanded => untriedMoves.Count == 0;
    public bool IsTerminalNode => GameState.IsTerminal();

    /// <summary>
    /// Expands the node by creating one new child node from an untried move.
    /// </summary>
    public Node Expand()
    {
        if (untriedMoves.Count == 0)
            throw new InvalidOperationException("Cannot expand a fully expanded node.");

        GameAction move = untriedMoves[random.Next(untriedMoves.Count)];
        untriedMoves.Remove(move);

        MCTSGameState nextState = GameState.ApplyMove(move);
        Node childNode = new Node(nextState, this, move);
        Children.Add(childNode);
        return childNode;
    }

    /// <summary>
    /// Updates this node's statistics from a simulation result.
    /// The result should be from the perspective of the player who made the move to reach this state.
    /// </summary>
    public void Update(GameResult result)
    {
        Visits++;
        // Assuming result.Score is from the perspective of the current player at this node's state
        // If MCTS tracks wins for the player *making* the move TO this state, then Parent.GameState.CurrentPlayerIndex matters.
        // For now, let's assume the score directly contributes.
        Wins += result.Score;
    }

    /// <summary>
    /// Calculates the UCT (Upper Confidence Bound 1 applied to Trees) value for this node.
    /// explorationParameter (C) controls the trade-off between exploitation and exploration.
    /// </summary>
    public double UCTValue(double explorationParameter)
    {
        if (Visits == 0)
        {
            return double.MaxValue; // Prioritize unvisited nodes
        }
        // Exploitation term: average win rate
        double exploitationTerm = Wins / Visits;
        // Exploration term
        double explorationTerm =
            explorationParameter * Math.Sqrt(Math.Log(Parent?.Visits ?? Visits) / Visits);

        return exploitationTerm + explorationTerm;
    }

    public override string ToString()
    {
        return $"[Move: {Move}, Wins/Visits: {Wins}/{Visits}, UCT: {UCTValue(Math.Sqrt(2))}]"; // UCT with common C = sqrt(2)
    }
}
