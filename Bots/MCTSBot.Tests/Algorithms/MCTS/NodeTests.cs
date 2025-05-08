using System;
using System.Linq;
using MCTSBot.Algorithms.MCTS;
using MCTSBot.Models;
using Xunit;

namespace MCTSBot.Tests.Algorithms.MCTS;

public class NodeTests
{
    private MCTSGameState CreateSimpleGameState()
    {
        // Corresponds to map:
        // E P E  (E=Empty, P=Pellet)
        // W . E  (W=Wall, .=Player Start)
        // E Z E  (Z=Escape Zone)
        var map = new int[,]
        {
            {
                MCTSGameState.CellTypeEmpty,
                MCTSGameState.CellTypePellet,
                MCTSGameState.CellTypeEmpty,
            },
            {
                MCTSGameState.CellTypeWall,
                MCTSGameState.CellTypeEmpty,
                MCTSGameState.CellTypeEmpty,
            },
            {
                MCTSGameState.CellTypeEmpty,
                MCTSGameState.CellTypeEscapeZone,
                MCTSGameState.CellTypeEmpty,
            },
        };

        return new MCTSGameState(map, 1, 1, 0, 100); // Player at (1,1)
    }

    [Fact]
    public void Node_Initialization_Correct()
    {
        var gameState = CreateSimpleGameState();
        var node = new Node(gameState);

        Assert.Same(gameState, node.GameState);
        Assert.Null(node.Parent);
        Assert.Null(node.Move);
        Assert.Equal(0, node.Visits);
        Assert.Equal(0, node.Wins);
        Assert.False(node.IsTerminalNode);

        // Player at (1,1) can move R, D, N (Up is pellet, Left is wall)
        // Possible moves: Right, Down, DoNothing. From MCTSGameState: Up (to pellet), Down (to escape), Right, DoNothing (Left is wall)
        // MCTSGameState.GetPossibleMoves() for (1,1) on simple map:
        // Up (1,0) -> Pellet YES -> GameAction.MoveUp
        // Down (1,2) -> EscapeZone YES -> GameAction.MoveDown
        // Left (0,1) -> Wall NO
        // Right (2,1) -> Empty YES -> GameAction.MoveRight
        // DoNothing YES -> GameAction.DoNothing
        // Expected untried moves: Up, Down, Right, DoNothing.
        Assert.Equal(4, gameState.GetPossibleMoves().Count);
        Assert.False(node.IsFullyExpanded);
    }

    [Fact]
    public void Node_Expand_CreatesChildAndRemovesUntriedMove()
    {
        var gameState = CreateSimpleGameState();
        var parentNode = new Node(gameState);
        int initialUntriedCount = parentNode.GameState.GetPossibleMoves().Count;

        Assert.True(initialUntriedCount > 0);

        Node childNode = parentNode.Expand();

        Assert.NotNull(childNode);
        Assert.Same(parentNode, childNode.Parent);
        Assert.Contains(childNode, parentNode.Children);
        Assert.NotNull(childNode.Move);
        Assert.True(parentNode.GameState.GetPossibleMoves().Contains(childNode.Move.Value));

        // After expand, one move is tried, but untriedMoves list is internal to Node.
        // We check IsFullyExpanded or count children vs possible moves.
        if (initialUntriedCount == 1)
        {
            Assert.True(parentNode.IsFullyExpanded);
        }
        else
        {
            Assert.False(parentNode.IsFullyExpanded);
        }

        Assert.Equal(1, parentNode.Children.Count);
    }

    [Fact]
    public void Node_Expand_AllMovesResultsInFullyExpanded()
    {
        var gameState = CreateSimpleGameState();
        var node = new Node(gameState);
        int moveCount = node.GameState.GetPossibleMoves().Count;

        for (int i = 0; i < moveCount; i++)
        {
            Assert.False(node.IsFullyExpanded);
            node.Expand();
        }

        Assert.True(node.IsFullyExpanded);
        Assert.Equal(moveCount, node.Children.Count);
    }

    [Fact]
    public void Node_Expand_ThrowsIfAlreadyFullyExpanded()
    {
        var gameState = CreateSimpleGameState();
        var node = new Node(gameState);
        int moveCount = node.GameState.GetPossibleMoves().Count;

        for (int i = 0; i < moveCount; i++)
        {
            node.Expand();
        }

        Assert.True(node.IsFullyExpanded);
        Assert.Throws<InvalidOperationException>(() => node.Expand());
    }

    [Fact]
    public void Node_Update_IncrementsVisitsAndAddsWins()
    {
        var gameState = CreateSimpleGameState();
        var node = new Node(gameState);
        var gameResult = new GameResult(10, false, false);

        node.Update(gameResult);

        Assert.Equal(1, node.Visits);
        Assert.Equal(10, node.Wins);

        node.Update(gameResult); // Call again
        Assert.Equal(2, node.Visits);
        Assert.Equal(20, node.Wins);
    }

    [Fact]
    public void Node_UCTValue_PrioritizesUnvisitedNodes()
    {
        var parentGameState = CreateSimpleGameState();
        var parentNode = new Node(parentGameState);

        // Expand once to have a child and to set parentNode.Visits > 0 after a playout/backprop simulation
        var childGameState = parentGameState.ApplyMove(GameAction.MoveRight);
        var childNode = new Node(childGameState, parentNode, GameAction.MoveRight);
        parentNode.Children.Add(childNode);

        // Simulate parent visit
        parentNode.Update(new GameResult(1, false, false));

        Assert.Equal(1, parentNode.Visits);
        Assert.Equal(0, childNode.Visits); // Child is unvisited
        Assert.Equal(double.MaxValue, childNode.UCTValue(1.414));
    }

    [Fact]
    public void Node_UCTValue_CalculatesCorrectly()
    {
        var parentGameState = CreateSimpleGameState();
        var parentNode = new Node(parentGameState);

        var childGameState = parentGameState.ApplyMove(GameAction.MoveUp); // Move to pellet
        var childNode = new Node(childGameState, parentNode, GameAction.MoveUp);
        parentNode.Children.Add(childNode);

        // Simulate visits and wins
        parentNode.Update(new GameResult(10, false, false)); // Parent visited once, total score 10
        parentNode.Update(new GameResult(5, false, false)); // Parent visited again, total score 15

        childNode.Update(new GameResult(8, false, false)); // Child visited once, score 8

        Assert.Equal(2, parentNode.Visits);
        Assert.Equal(1, childNode.Visits);
        Assert.Equal(8, childNode.Wins);

        double explorationParameter = Math.Sqrt(2);
        double expectedUct =
            (childNode.Wins / childNode.Visits)
            + explorationParameter * Math.Sqrt(Math.Log(parentNode.Visits) / childNode.Visits);

        Assert.Equal(expectedUct, childNode.UCTValue(explorationParameter), 5); // Precision up to 5 decimal places
    }

    [Fact]
    public void Node_IsTerminalNode_ReflectsGameState()
    {
        var gameState = CreateSimpleGameState();
        var node = new Node(gameState);
        Assert.False(node.IsTerminalNode); // Initial state is not terminal

        // Simulate a move to an escape zone
        var terminalState = gameState.ApplyMove(GameAction.MoveDown); // MoveDown leads to escape zone in simple map
        var terminalNode = new Node(terminalState);
        Assert.True(terminalNode.IsTerminalNode);
    }
}
