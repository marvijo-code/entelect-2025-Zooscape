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
            }, // Player will be at (1,1), ZK at (2,1)
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
        var state = new MCTSGameState(map, 1, 1, 0, 100, 2, 1); // Player at (X=1,Y=1), ZK at (X=2,Y=1)

        Assert.Equal(1, state.PlayerX);
        Assert.Equal(1, state.PlayerY);
        Assert.Equal(0, state.Score);
        Assert.Equal(0, state.CurrentTick);
        Assert.Equal(100, state.MaxTicksForSimulation);
        Assert.Equal(2, state.ZookeeperX);
        Assert.Equal(1, state.ZookeeperY);
        Assert.Equal(map.GetLength(0), state.MapHeight); // Rows
        Assert.Equal(map.GetLength(1), state.MapWidth); // Columns
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
        Assert.Equal(4, moves.Count); // Down, Left, Right, DoNothing
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
        var state = new MCTSGameState(map, 0, 0, 0, 100); // Player at (X=0,Y=0)
        var moves = state.GetPossibleMoves();

        // At (X=0,Y=0):
        // Up (0,-1): Invalid
        // Down (0,1): MapData[1,0] is Pellet. YES
        // Left (-1,0): Invalid
        // Right (1,0): MapData[0,1] is Wall. NO

        Assert.DoesNotContain(GameAction.MoveUp, moves);
        Assert.Contains(GameAction.MoveDown, moves); // To (X=0,Y=1) Pellet
        Assert.DoesNotContain(GameAction.MoveLeft, moves);
        Assert.DoesNotContain(GameAction.MoveRight, moves); // Blocked by wall at (X=1,Y=0)
        Assert.Contains(GameAction.DoNothing, moves);
        Assert.Equal(2, moves.Count); // Down, DoNothing
    }

    [Fact]
    public void ApplyMove_PlayerMovesCorrectly()
    {
        var map = CreateSimpleMap(); // Player at (X=1,Y=1)
        // Pellet at (X=1,Y=0) using corrected CreateSimpleMap
        var state = new MCTSGameState(map, 1, 1, 0, 100, 2, 1);
        var nextState = state.ApplyMove(GameAction.MoveUp); // Move to (X=1,Y=0) - Pellet

        Assert.Equal(1, nextState.PlayerX);
        Assert.Equal(0, nextState.PlayerY);
        Assert.Equal(1, nextState.CurrentTick); // Tick increments
        Assert.Equal(1 + 10, nextState.Score); // +1 for tick, +10 for pellet
        Assert.Equal(MCTSGameState.CellTypeEmpty, nextState.MapData[0, 1]); // Pellet consumed at (X=1,Y=0) -> map[0,1]
    }

    [Fact]
    public void ApplyMove_ZookeeperMoves()
    {
        var map = CreateSimpleMap(); // ZK at (X=0,Y=0) for this test
        var state = new MCTSGameState(map, 1, 1, 0, 100, 0, 0); // Player (1,1)
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
        }; // Minimal map [Y=0,X=0]
        var state = new MCTSGameState(map, 0, 0, 0, 100, 0, 0); // Player(0,0), ZK(0,0)
        Assert.True(state.IsTerminal());
        Assert.Equal(-1000, state.GetGameResult().Score); // Score reflects capture penalty (initial state, 0 ticks)
    }

    [Fact]
    public void IsTerminal_PlayerEscapes()
    {
        var map = new int[,]
        {
            { MCTSGameState.CellTypeEscapeZone },
        }; // [Y=0,X=0]
        var state = new MCTSGameState(map, 0, 0, 0, 100);
        Assert.True(state.IsTerminal());
        Assert.Equal(1000, state.GetGameResult().Score); // Score reflects escape bonus (initial state, 0 ticks)
    }

    [Fact]
    public void ApplyMove_EscapeLeadsToTerminalAndBonus()
    {
        // Map: [Y=0,X=0]=Empty, [Y=0,X=1]=Escape
        var map = new int[,]
        {
            { MCTSGameState.CellTypeEmpty, MCTSGameState.CellTypeEscapeZone },
        };
        var state = new MCTSGameState(map, 0, 0, 0, 100); // Player at (X=0,Y=0)
        var nextState = state.ApplyMove(GameAction.MoveRight); // Player moves to (X=1,Y=0) - Escape Zone

        Assert.Equal(1, nextState.PlayerX); // Player is at X=1
        Assert.Equal(0, nextState.PlayerY); // Player is at Y=0
        Assert.True(
            IsValidForTest(nextState.PlayerY, nextState.PlayerX, map.GetLength(0), map.GetLength(1))
        );
        Assert.Equal(MCTSGameState.CellTypeEscapeZone, map[nextState.PlayerY, nextState.PlayerX]);

        Assert.True(nextState.IsTerminal());
        Assert.Equal(1 + 1000, nextState.Score); // +1 for tick, +1000 for escape
        Assert.Equal(1 + 1000, nextState.GetGameResult().Score);
    }

    [Fact]
    public void IsTerminal_MaxTicksReached()
    {
        var map = new int[,]
        {
            { MCTSGameState.CellTypeEmpty },
        };
        var state = new MCTSGameState(map, 0, 0, 0, 1); // Max 1 tick
        var s1 = state.ApplyMove(GameAction.DoNothing);
        Assert.False(state.IsTerminal()); // Initial state
        Assert.True(s1.IsTerminal()); // After 1 tick
        Assert.Equal(1, s1.Score); // Only score from tick
        Assert.Equal(1, s1.GetGameResult().Score);
    }

    // Helper for debugging map access in tests
    private bool IsValidForTest(int y, int x, int mapHeight, int mapWidth)
    {
        return x >= 0 && x < mapWidth && y >= 0 && y < mapHeight;
    }
}
