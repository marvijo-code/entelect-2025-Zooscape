using System.Diagnostics;
using MCTSo4.Algorithms.MCTS;
using MCTSo4.Models;
using Xunit;

namespace MCTSo4.Tests;

/// <summary>
/// Tests for parallel MCTS implementation
/// </summary>
public class ParallelMctsTests
{
    /// <summary>
    /// Test that parallel MCTS returns valid moves
    /// </summary>
    [Fact]
    public void ParallelMCTS_FindBestMove_ReturnsValidMove()
    {
        // Arrange
        var state = new MCTSGameState();
        var botId = Guid.NewGuid();
        var parameters = new BotParameters
        {
            MctsIterations = 100,
            MctsDepth = 10,
            MaxTimePerMoveMs = 50, // Lower for test
            ExplorationConstant = 1.41,
            ProgressiveWideningBase = 2.0,
            ProgressiveWideningExponent = 0.5,
        };
        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        var move = ParallelMctsAlgorithm.FindBestMove(state, botId, parameters, stopwatch);
        stopwatch.Stop();

        // Assert
        Assert.True(Enum.IsDefined(typeof(Move), move), "Should return a valid move");
    }

    /// <summary>
    /// Test that thread-safe node selection works correctly
    /// </summary>
    [Fact]
    public void ThreadSafeNode_BestChild_ReturnsBestUctNode()
    {
        // Arrange
        var state = new MCTSGameState();
        var botId = Guid.NewGuid();
        var parameters = new BotParameters { ExplorationConstant = 1.41 };

        var parentNode = new ThreadSafeNode(
            state,
            null,
            null,
            parameters.ExplorationConstant,
            botId,
            parameters
        );

        // Create child nodes with different scores
        var child1 = new ThreadSafeNode(
            state,
            parentNode,
            Move.Up,
            parameters.ExplorationConstant,
            botId,
            parameters
        );
        var child2 = new ThreadSafeNode(
            state,
            parentNode,
            Move.Down,
            parameters.ExplorationConstant,
            botId,
            parameters
        );
        var child3 = new ThreadSafeNode(
            state,
            parentNode,
            Move.Left,
            parameters.ExplorationConstant,
            botId,
            parameters
        );

        // Manually set stats to simulate node selection
        parentNode.UpdateStats(100); // Parent visited once with score 100

        child1.UpdateStats(50); // Child 1: 1 visit, score 50
        child2.UpdateStats(75); // Child 2: 1 visit, score 75 (best)
        child3.UpdateStats(25); // Child 3: 1 visit, score 25

        // Add to parent
        parentNode.AddChild(child1);
        parentNode.AddChild(child2);
        parentNode.AddChild(child3);

        // Act
        var bestChild = parentNode.BestChild();

        // Assert
        Assert.Equal(Move.Down, bestChild.Move);
    }

    /// <summary>
    /// Test that virtual loss works correctly
    /// </summary>
    [Fact]
    public void ThreadSafeNode_VirtualLoss_DiscouragesSelection()
    {
        // Arrange
        var state = new MCTSGameState();
        var botId = Guid.NewGuid();
        var parameters = new BotParameters { ExplorationConstant = 1.41, VirtualLossCount = 5 };

        var parentNode = new ThreadSafeNode(
            state,
            null,
            null,
            parameters.ExplorationConstant,
            botId,
            parameters
        );

        // Create identical child nodes
        var child1 = new ThreadSafeNode(
            state,
            parentNode,
            Move.Up,
            parameters.ExplorationConstant,
            botId,
            parameters
        );
        var child2 = new ThreadSafeNode(
            state,
            parentNode,
            Move.Down,
            parameters.ExplorationConstant,
            botId,
            parameters
        );

        // Give both identical stats
        parentNode.UpdateStats(100);
        child1.UpdateStats(50);
        child2.UpdateStats(50);

        // Add virtual loss to child1
        child1.AddVirtualLoss();

        // Add to parent
        parentNode.AddChild(child1);
        parentNode.AddChild(child2);

        // Act
        var bestChild = parentNode.BestChild();

        // Assert - child2 should be selected since child1 has virtual loss
        Assert.Equal(Move.Down, bestChild.Move);

        // Remove virtual loss
        child1.RemoveVirtualLoss();

        // Now they should be equal, and selection could depend on order
        var ucb1 = child1.UctValue();
        var ucb2 = child2.UctValue();

        // Assert
        Assert.True(
            Math.Abs(ucb1 - ucb2) < 0.001,
            "UCB values should be nearly equal after removing virtual loss"
        );
    }

    /// <summary>
    /// Test progressive widening controls expansion correctly
    /// </summary>
    [Fact]
    public void ThreadSafeNode_ProgressiveWidening_ControlsExpansion()
    {
        // Arrange
        var state = new MCTSGameState();
        var botId = Guid.NewGuid();
        var parameters = new BotParameters
        {
            ProgressiveWideningBase = 1.0,
            ProgressiveWideningExponent = 0.5, // For N visits, allow sqrt(N) children
        };

        var node = new ThreadSafeNode(
            state,
            null,
            null,
            parameters.ExplorationConstant,
            botId,
            parameters
        );

        // Act & Assert - with 0 visits, the node should not be fully expanded
        Assert.False(node.IsFullyExpanded, "Node with 0 visits should not be fully expanded");

        // Simulate visits
        for (int i = 0; i < 4; i++)
        {
            node.UpdateStats(10);
        }

        // With 4 visits and PW of 1.0 * sqrt(4) = 2, node should allow 2 children
        // Since no children exist yet, it should not be fully expanded
        Assert.False(
            node.IsFullyExpanded,
            "Node with 4 visits and no children should not be fully expanded"
        );

        // Add two children
        bool gotAction1 = node.TryGetUntriedAction(out var action1);
        bool gotAction2 = node.TryGetUntriedAction(out var action2);

        Assert.True(gotAction1, "Should get first action");
        Assert.True(gotAction2, "Should get second action");

        // Create and add two child nodes
        var child1 = new ThreadSafeNode(
            state,
            node,
            action1,
            parameters.ExplorationConstant,
            botId,
            parameters
        );
        var child2 = new ThreadSafeNode(
            state,
            node,
            action2,
            parameters.ExplorationConstant,
            botId,
            parameters
        );
        node.AddChild(child1);
        node.AddChild(child2);

        // Now the node should be fully expanded according to progressive widening
        Assert.True(
            node.IsFullyExpanded,
            "Node with 4 visits and 2 children should be fully expanded"
        );

        // Add more visits to allow more children
        for (int i = 0; i < 5; i++)
        {
            node.UpdateStats(10);
        }

        // Now with 9 visits, PW = 1.0 * sqrt(9) = 3, so node should allow 3 children
        Assert.False(
            node.IsFullyExpanded,
            "Node with 9 visits and 2 children should allow more expansion"
        );

        // Should be able to get one more action
        bool gotAction3 = node.TryGetUntriedAction(out var action3);
        Assert.True(gotAction3, "Should get third action with increased visits");
    }
}
