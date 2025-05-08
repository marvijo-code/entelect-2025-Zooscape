using System.Linq;
using MCTSBot.Models;
using Xunit;

namespace MCTSBot.Tests.Models;

public class MCTSGameStateTests
{
    private int[,] CreateSimpleMap()
    {
        // MapData[Y, X] (row, col)
        // Player at (X=1,Y=1), Pellet at (X=1,Y=0), Wall at (X=0,Y=1), Escape at (X=1,Y=2)
        return new int[,]
        {
            /* X=0, X=1, X=2 */
            /*Y=0*/{
                MCTSGameState.CellTypeEmpty,
                MCTSGameState.CellTypePellet,
                MCTSGameState.CellTypeEmpty,
            },
            /*Y=1*/{
                MCTSGameState.CellTypeWall,
                MCTSGameState.CellTypeEmpty,
                MCTSGameState.CellTypeEmpty,
            },
            /*Y=2*/{
                MCTSGameState.CellTypeEmpty,
                MCTSGameState.CellTypeEscapeZone,
                MCTSGameState.CellTypeEmpty,
            },
        };
    }

    [Fact]
    public void Constructor_InitializesPropertiesCorrectly()
    {
        var map = CreateSimpleMap();
        var state = new MCTSGameState(map, 1, 1, 0, 100, 2, 1);

        Assert.Equal(1, state.PlayerX);
        Assert.Equal(1, state.PlayerY);
        Assert.Equal(0, state.Score);
        Assert.Equal(0, state.CurrentTick);
        Assert.Equal(100, state.MaxTicksForSimulation);
        Assert.Equal(2, state.ZookeeperX);
        Assert.Equal(1, state.ZookeeperY);
        Assert.Equal(map.GetLength(0), state.MapHeight);
        Assert.Equal(map.GetLength(1), state.MapWidth);
    }

    [Fact]
    public void Clone_CreatesDeepCopy()
    {
        var map = CreateSimpleMap();
        var originalState = new MCTSGameState(map, 1, 1, 0, 100);
        var clonedState = originalState.Clone();

        Assert.NotSame(originalState.MapData, clonedState.MapData);
        Assert.Equal(originalState.MapData, clonedState.MapData);
        Assert.Equal(originalState.PlayerX, clonedState.PlayerX);
        Assert.Equal(originalState.Score, clonedState.Score);

        // Modify original map after clone, clone should not change
        originalState.MapData[0, 0] = MCTSGameState.CellTypeWall;
        Assert.NotEqual(originalState.MapData[0, 0], clonedState.MapData[0, 0]);
    }

    [Fact]
    public void GetPossibleMoves_ReturnsCorrectMoves()
    {
        // MapData[Y,X]
        var map = new int[,]
        {
            /*Y=0*/{
                MCTSGameState.CellTypeEmpty,
                MCTSGameState.CellTypeWall,
                MCTSGameState.CellTypeEmpty,
            },
            /*Y=1*/{
                MCTSGameState.CellTypePellet,
                MCTSGameState.CellTypeEmpty,
                MCTSGameState.CellTypeEmpty,
            },
            /*Y=2*/{
                MCTSGameState.CellTypeEmpty,
                MCTSGameState.CellTypeEmpty,
                MCTSGameState.CellTypeEmpty,
            },
        };
        // Player at (X=1,Y=1) (center of this map)
        var state = new MCTSGameState(map, 1, 1, 0, 100);

        var moves = state.GetPossibleMoves();

        // At (X=1,Y=1):
        // Up (1,0): MapData[0,1] is Wall. NO
        // Down (1,2): MapData[2,1] is Empty. YES
        // Left (0,1): MapData[1,0] is Pellet. YES
        // Right (2,1): MapData[1,2] is Empty. YES

        Assert.DoesNotContain(GameAction.MoveUp, moves);
        Assert.Contains(GameAction.MoveDown, moves);
        Assert.Contains(GameAction.MoveLeft, moves);
        Assert.Contains(GameAction.MoveRight, moves);
        Assert.Contains(GameAction.DoNothing, moves);
        Assert.Equal(4, moves.Count);
    }

    [Fact]
    public void GetPossibleMoves_RespectsWalls()
    {
        // MapData[Y,X]
        var map = new int[,]
        {
            //       X=0                     X=1                        X=2
            /*Y=0*/{
                MCTSGameState.CellTypeEmpty,
                MCTSGameState.CellTypeWall,
                MCTSGameState.CellTypeEmpty,
            },
            /*Y=1*/{
                MCTSGameState.CellTypePellet,
                MCTSGameState.CellTypeEmpty,
                MCTSGameState.CellTypeEmpty,
            },
            /*Y=2*/{
                MCTSGameState.CellTypeEmpty,
                MCTSGameState.CellTypeEmpty,
                MCTSGameState.CellTypeEmpty,
            },
        };
        var state = new MCTSGameState(map, 0, 0, 0, 100);
        var moves = state.GetPossibleMoves();

        // At (X=0,Y=0):
        // Up (0,-1): Invalid
        // Down (0,1): MapData[1,0] is Pellet. YES
        // Left (-1,0): Invalid
        // Right (1,0): MapData[0,1] is Wall. NO

        Assert.DoesNotContain(GameAction.MoveUp, moves);
        Assert.Contains(GameAction.MoveDown, moves);
        Assert.DoesNotContain(GameAction.MoveLeft, moves);
        Assert.DoesNotContain(GameAction.MoveRight, moves);
        Assert.Contains(GameAction.DoNothing, moves);
        Assert.Equal(2, moves.Count);
    }

    [Fact]
    public void ApplyMove_PlayerMovesCorrectly()
    {
        var map = CreateSimpleMap();
        // Pellet at (X=1,Y=0) using corrected CreateSimpleMap
        var state = new MCTSGameState(map, 1, 1, 0, 100, 2, 1);
        var nextState = state.ApplyMove(GameAction.MoveUp);

        Assert.Equal(1, nextState.PlayerX);
        Assert.Equal(0, nextState.PlayerY);
        Assert.Equal(1, nextState.CurrentTick);
        Assert.Equal(1 + 10, nextState.Score);
        Assert.Equal(MCTSGameState.CellTypeEmpty, nextState.MapData[0, 1]);
    }

    [Fact]
    public void ApplyMove_ZookeeperMoves()
    {
        var map = CreateSimpleMap();
        var state = new MCTSGameState(map, 1, 1, 0, 100, 0, 0);
        var s1 = state.ApplyMove(GameAction.DoNothing);
        var s2 = s1.ApplyMove(GameAction.DoNothing);
        var s3 = s2.ApplyMove(GameAction.DoNothing);
        var s4 = s3.ApplyMove(GameAction.DoNothing);

        bool zookeeperMoved = (
            s4.ZookeeperX != state.ZookeeperX || s4.ZookeeperY != state.ZookeeperY
        );
        bool zookeeperInBounds =
            s4.IsValid(s4.ZookeeperX, s4.ZookeeperY)
            && s4.MapData[s4.ZookeeperY, s4.ZookeeperX] != MCTSGameState.CellTypeWall;
        Assert.True(zookeeperMoved || (s4.ZookeeperX == 0 && s4.ZookeeperY == 0));
        Assert.True(zookeeperInBounds);
    }

    [Fact]
    public void IsTerminal_PlayerCaptured()
    {
        var map = new int[,]
        {
            { MCTSGameState.CellTypeEmpty },
        };
        var state = new MCTSGameState(map, 0, 0, 0, 100, 0, 0);
        Assert.True(state.IsTerminal());
        Assert.Equal(-1000, state.GetGameResult().Score);
    }

    [Fact]
    public void ApplyMove_CaptureLeadsToTerminalAndPenalty()
    {
        var map = new int[,]
        {
            { MCTSGameState.CellTypeEmpty, MCTSGameState.CellTypeEmpty },
            { MCTSGameState.CellTypeEmpty, MCTSGameState.CellTypeEmpty },
        };
        // Player at (0,0), Zookeeper at (0,1). Player moves to (0,1)
        var state = new MCTSGameState(map, 0, 0, 0, 100, 0, 1);
        var nextState = state.ApplyMove(GameAction.MoveDown);

        Assert.True(nextState.IsTerminal());
        Assert.Equal(1 - 1000, nextState.Score);
        Assert.Equal(1 - 1000, nextState.GetGameResult().Score);
    }

    [Fact]
    public void IsTerminal_PlayerEscapes()
    {
        var map = new int[,]
        {
            { MCTSGameState.CellTypeEscapeZone },
        };
        var state = new MCTSGameState(map, 0, 0, 0, 100);
        Assert.True(state.IsTerminal());
        Assert.Equal(1000, state.GetGameResult().Score);
    }

    [Fact]
    public void ApplyMove_EscapeLeadsToTerminalAndBonus()
    {
        // Map: [Y=0,X=0]=Empty, [Y=0,X=1]=Escape
        var map = new int[,]
        {
            { MCTSGameState.CellTypeEmpty, MCTSGameState.CellTypeEscapeZone },
        };
        var state = new MCTSGameState(map, 0, 0, 0, 100);
        var nextState = state.ApplyMove(GameAction.MoveRight);

        Assert.Equal(1, nextState.PlayerX);
        Assert.Equal(0, nextState.PlayerY);
        Assert.True(
            IsValidForTest(nextState.PlayerY, nextState.PlayerX, map.GetLength(0), map.GetLength(1))
        );
        Assert.Equal(MCTSGameState.CellTypeEscapeZone, map[nextState.PlayerY, nextState.PlayerX]);

        Assert.True(nextState.IsTerminal());
        Assert.Equal(1 + 1000, nextState.Score);
        Assert.Equal(1 + 1000, nextState.GetGameResult().Score);
    }

    [Fact]
    public void IsTerminal_MaxTicksReached()
    {
        var map = new int[,]
        {
            { MCTSGameState.CellTypeEmpty },
        };
        var state = new MCTSGameState(map, 0, 0, 0, 1);
        var s1 = state.ApplyMove(GameAction.DoNothing);
        Assert.False(state.IsTerminal());
        Assert.True(s1.IsTerminal());
        Assert.Equal(1, s1.Score);
        Assert.Equal(1, s1.GetGameResult().Score);
    }

    // Helper for debugging map access in tests
    private bool IsValidForTest(int y, int x, int mapHeight, int mapWidth)
    {
        return x >= 0 && x < mapWidth && y >= 0 && y < mapHeight;
    }
}
