using System.Linq;
using MCTSBot.Algorithms.MCTS;
using MCTSBot.Models;
using Xunit;

namespace MCTSBot.Tests.Algorithms.MCTS;

public class MCTSAlgorithmTests
{
    private MCTSGameState CreateSimpleGameState(bool withPellet = true, bool withEscape = true)
    {
        var map = new int[3, 3]; // 3x3 map
        for (int r = 0; r < 3; r++)
        for (int c = 0; c < 3; c++)
            map[r, c] = MCTSGameState.CellTypeEmpty;

        // Player at (1,1)
        // Pellet at (1,0) if withPellet
        // Escape at (1,2) if withEscape

        if (withPellet)
            map[0, 1] = MCTSGameState.CellTypePellet; // Y=0, X=1
        if (withEscape)
            map[2, 1] = MCTSGameState.CellTypeEscapeZone; // Y=2, X=1

        // Wall at (0,1) if no pellet, to block UP if nothing is there
        if (!withPellet)
            map[0, 1] = MCTSGameState.CellTypeWall;

        return new MCTSGameState(map, 1, 1, 0, 100); // Player at (X=1, Y=1)
    }

    [Fact]
    public void FindBestMove_ReturnsValidAction_SimpleState()
    {
        var gameState = CreateSimpleGameState(); // Player at (1,1), pellet at (1,0), escape at (1,2)
        // Possible: Up (pellet), Down (escape), Left, Right, DoNothing
        var mcts = new MCTSAlgorithm(explorationParameter: 1.414);

        // With few iterations, it might pick any valid move.
        // We just want to ensure it returns *something* valid from the possible moves.
        var bestMove = mcts.FindBestMove(gameState, iterations: 10);

        var possibleMoves = gameState.GetPossibleMoves();
        Assert.Contains(bestMove, possibleMoves);
    }

    [Fact]
    public void FindBestMove_PrefersWinningMove_EscapeAvailable()
    {
        // Game state where escape is one move away and clearly best
        var map = new int[,]
        {
            { MCTSGameState.CellTypeWall, MCTSGameState.CellTypeWall, MCTSGameState.CellTypeWall },
            {
                MCTSGameState.CellTypeEmpty,
                MCTSGameState.CellTypeEmpty,
                MCTSGameState.CellTypeEscapeZone,
            }, // Player at (1,1), Escape at (2,1)
            { MCTSGameState.CellTypeWall, MCTSGameState.CellTypeWall, MCTSGameState.CellTypeWall },
        };
        var gameState = new MCTSGameState(map, 1, 1, 0, 10);
        var mcts = new MCTSAlgorithm(explorationParameter: System.Math.Sqrt(2));

        // After enough iterations, it should pick MoveRight to escape
        var bestMove = mcts.FindBestMove(gameState, iterations: 100);

        Assert.Equal(GameAction.MoveRight, bestMove);
    }

    [Fact]
    public void FindBestMove_PrefersPellet_WhenEscapeIsFar()
    {
        // Map: Player(1,1), Pellet(0,1), Escape(2,2) (far)
        var map = new int[3, 3];
        for (int r = 0; r < 3; r++)
        for (int c = 0; c < 3; c++)
            map[r, c] = MCTSGameState.CellTypeEmpty;
        map[1, 0] = MCTSGameState.CellTypePellet; // Pellet at X=0, Y=1
        map[2, 2] = MCTSGameState.CellTypeEscapeZone; // Escape at X=2, Y=2

        var gameState = new MCTSGameState(map, 1, 1, 0, 50);
        var mcts = new MCTSAlgorithm(explorationParameter: 1.414);

        // Should prefer MoveLeft to get the pellet, as escape is further and incurs more ticks.
        var bestMove = mcts.FindBestMove(gameState, iterations: 200); // More iterations to be sure

        Assert.Equal(GameAction.MoveLeft, bestMove);
    }

    [Fact]
    public void FindBestMove_HandlesNoPossibleMovesGracefully()
    {
        // Player is completely walled in.
        var map = new int[,]
        {
            { MCTSGameState.CellTypeWall, MCTSGameState.CellTypeWall, MCTSGameState.CellTypeWall },
            { MCTSGameState.CellTypeWall, MCTSGameState.CellTypeEmpty, MCTSGameState.CellTypeWall }, // Player at (1,1)
            { MCTSGameState.CellTypeWall, MCTSGameState.CellTypeWall, MCTSGameState.CellTypeWall },
        };
        var gameState = new MCTSGameState(map, 1, 1, 0, 10);
        var mcts = new MCTSAlgorithm();

        // Only DoNothing is possible if GetPossibleMoves always includes it.
        // If GetPossibleMoves can be empty, MCTS should handle it.
        // Current MCTSGameState.GetPossibleMoves() adds DoNothing if not terminal.
        // If the state became terminal (e.g. ZK catches player), then GetPossibleMoves is empty.

        var bestMove = mcts.FindBestMove(gameState, iterations: 10);
        Assert.Equal(GameAction.DoNothing, bestMove); // Only DoNothing is possible
    }

    [Fact]
    public void FindBestMove_HandlesAlreadyTerminalState()
    {
        var map = new int[,]
        {
            { MCTSGameState.CellTypeEscapeZone },
        }; // Player starts on escape
        var gameState = new MCTSGameState(map, 0, 0, 0, 10);
        var mcts = new MCTSAlgorithm();

        // If root is terminal, it should still return a default action like DoNothing.
        var bestMove = mcts.FindBestMove(gameState, iterations: 10);
        Assert.Equal(GameAction.DoNothing, bestMove); // No moves to make from a terminal state root
    }
}
