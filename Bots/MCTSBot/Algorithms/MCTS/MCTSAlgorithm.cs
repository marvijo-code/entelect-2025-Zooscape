using System;
using System.Collections.Generic;
using System.Linq;
using MCTSBot.Models;

namespace MCTSBot.Algorithms.MCTS;

public class MCTSAlgorithm
{
    private readonly double explorationParameter;
    private static Random random = new Random();

    public MCTSAlgorithm(double explorationParameter = 1.414) // Approx. Sqrt(2)
    {
        this.explorationParameter = explorationParameter;
    }

    public GameAction FindBestMove(MCTSGameState initialGameState, int iterations)
    {
        Node rootNode = new Node(initialGameState);

        for (int i = 0; i < iterations; i++)
        {
            Node promisingNode = SelectPromisingNode(rootNode);

            Node nodeToExplore = promisingNode;
            if (!promisingNode.IsTerminalNode && !promisingNode.IsFullyExpanded)
            {
                nodeToExplore = promisingNode.Expand();
            }

            GameResult playoutResult = SimulateRandomPlayout(nodeToExplore);
            Backpropagate(nodeToExplore, playoutResult);
        }

        Node? bestChild = rootNode
            .Children.OrderByDescending(c => c.Visits) // Most visited child is often a good heuristic
            // .ThenByDescending(c => c.Wins / (c.Visits == 0 ? 1 : c.Visits)) // Or highest win rate
            .FirstOrDefault();

        return bestChild?.Move ?? GameAction.DoNothing; // Default to DoNothing if no moves found
    }

    private Node SelectPromisingNode(Node rootNode)
    {
        Node node = rootNode;
        while (!node.IsTerminalNode && node.IsFullyExpanded)
        {
            node = node.Children.OrderByDescending(c => c.UCTValue(explorationParameter)).First();
        }
        return node;
    }

    private GameResult SimulateRandomPlayout(Node node)
    {
        MCTSGameState tempState = node.GameState.Clone();
        int simulationDepth = 0;
        const int maxSimulationDepth = 100; // Prevent excessively long simulations

        while (!tempState.IsTerminal() && simulationDepth < maxSimulationDepth)
        {
            List<GameAction> possibleMoves = tempState.GetPossibleMoves();
            if (possibleMoves.Count == 0)
            {
                // No moves possible, treat as a terminal state for simulation purposes
                break;
            }
            GameAction randomMove = possibleMoves[random.Next(possibleMoves.Count)];
            tempState = tempState.ApplyMove(randomMove);
            simulationDepth++;
        }
        return tempState.GetGameResult();
    }

    private void Backpropagate(Node node, GameResult result)
    {
        Node? tempNode = node;
        while (tempNode != null)
        {
            tempNode.Update(result);
            // If the game is strictly alternating, you might need to negate the score for the parent
            // or adjust based on whose turn it was. For now, assuming 'result.Score' is absolute or relative to current player.
            // Example for alternating game: if(tempNode.Parent != null) result.Score = -result.Score;
            tempNode = tempNode.Parent;
        }
    }
}
