using System;
using System.Collections.Generic;
using System.Linq;

namespace MCTSBot.Models;

/// <summary>
/// Represents the state of the game for the MCTS algorithm.
/// </summary>
public class MCTSGameState
{
    public const int CellTypeEmpty = 0;
    public const int CellTypeWall = 1;
    public const int CellTypePellet = 2;
    public const int CellTypePowerUp = 3; // Generic power-up
    public const int CellTypeEscapeZone = 4;
    public const int CellTypeZookeeper = 5;

    public int[,] MapData { get; private set; }
    public int MapWidth => MapData.GetLength(1); // Columns
    public int MapHeight => MapData.GetLength(0); // Rows

    public int PlayerX { get; private set; }
    public int PlayerY { get; private set; }
    public int Score { get; private set; }

    public int ZookeeperX { get; private set; } = -1; // -1 if no zookeeper or inactive
    public int ZookeeperY { get; private set; } = -1;

    public int CurrentTick { get; private set; }
    public int MaxTicksForSimulation { get; private set; }

    public int CurrentPlayerIndex { get; private set; } // 0 for our bot, 1 for zookeeper (conceptual for turn order)
    private static Random random = new Random();

    /// <summary>
    /// Constructor for initial game state translation.
    /// </summary>
    public MCTSGameState(
        int[,] initialMapData,
        int playerX,
        int playerY,
        int initialScore,
        int maxTicks,
        int initialZookeeperX = -1,
        int initialZookeeperY = -1
    )
    {
        MapData = (int[,])initialMapData.Clone(); // Ensure we have a copy
        PlayerX = playerX;
        PlayerY = playerY;
        Score = initialScore;
        CurrentTick = 0;
        MaxTicksForSimulation = maxTicks;
        CurrentPlayerIndex = 0; // Bot's turn

        if (initialZookeeperX != -1 && initialZookeeperY != -1)
        {
            ZookeeperX = initialZookeeperX;
            ZookeeperY = initialZookeeperY;
            if (IsValid(ZookeeperX, ZookeeperY) && MapData[ZookeeperY, ZookeeperX] != CellTypeWall) // Don't overwrite walls
            {
                // MapData[ZookeeperY, ZookeeperX] = CellTypeZookeeper; // Represent on map if needed, or just track coords
            }
            else
            {
                ZookeeperX = -1; // Invalid position
                ZookeeperY = -1;
            }
        }
    }

    /// <summary>
    /// Private constructor for cloning.
    /// </summary>
    private MCTSGameState(MCTSGameState source)
    {
        MapData = (int[,])source.MapData.Clone();
        PlayerX = source.PlayerX;
        PlayerY = source.PlayerY;
        Score = source.Score;
        ZookeeperX = source.ZookeeperX;
        ZookeeperY = source.ZookeeperY;
        CurrentTick = source.CurrentTick;
        MaxTicksForSimulation = source.MaxTicksForSimulation;
        CurrentPlayerIndex = source.CurrentPlayerIndex;
    }

    public MCTSGameState Clone()
    {
        return new MCTSGameState(this);
    }

    public bool IsValid(int x, int y)
    {
        return x >= 0 && x < MapWidth && y >= 0 && y < MapHeight;
    }

    public List<GameAction> GetPossibleMoves()
    {
        var moves = new List<GameAction>();
        if (IsTerminal())
            return moves; // No moves if terminal

        // Player moves
        if (IsValid(PlayerX, PlayerY - 1) && MapData[PlayerY - 1, PlayerX] != CellTypeWall)
            moves.Add(GameAction.MoveUp);
        if (IsValid(PlayerX, PlayerY + 1) && MapData[PlayerY + 1, PlayerX] != CellTypeWall)
            moves.Add(GameAction.MoveDown);
        if (IsValid(PlayerX - 1, PlayerY) && MapData[PlayerY, PlayerX - 1] != CellTypeWall)
            moves.Add(GameAction.MoveLeft);
        if (IsValid(PlayerX + 1, PlayerY) && MapData[PlayerY, PlayerX + 1] != CellTypeWall)
            moves.Add(GameAction.MoveRight);
        moves.Add(GameAction.DoNothing);

        return moves;
    }

    public MCTSGameState ApplyMove(GameAction action)
    {
        MCTSGameState nextState = null; // Initialize to null
        try
        {
            nextState = Clone();
            nextState.CurrentTick++; // Original line 120
            nextState.Score++; // Original line 121 - REPORTED ERROR LINE

            // Trigger MapData access for diagnostics if needed later by uncommenting:
            // int width = nextState.MapWidth;
            // int height = nextState.MapHeight;
        }
        catch (IndexOutOfRangeException ioex)
        {
            Console.WriteLine(
                $"!!! IndexOutOfRangeException CAUGHT EARLY in ApplyMove. Action: {action}"
            );
            Console.WriteLine(
                $"State before Clone: Tick: {this.CurrentTick}, Player: ({this.PlayerX},{this.PlayerY}), Map: {this.MapWidth}x{this.MapHeight}"
            );
            if (nextState != null) // nextState might be null if Clone() failed, or partially initialized
            {
                Console.WriteLine(
                    $"State after Clone attempt: CurrentTick (if set): {nextState.CurrentTick}"
                );
                // Safely check MapData and its dimensions
                if (nextState.MapData != null)
                {
                    Console.WriteLine(
                        $"Cloned MapData dimensions: {nextState.MapData.GetLength(0)}x{nextState.MapData.GetLength(1)}"
                    );
                }
                else
                {
                    Console.WriteLine("Cloned MapData is NULL.");
                }
            }
            else
            {
                Console.WriteLine(
                    "nextState is null, Clone() likely failed or error occurred before/during Clone assignment."
                );
            }
            Console.WriteLine(ioex.ToString());
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"!!! Exception CAUGHT EARLY in ApplyMove (NOT IndexOutOfRange). Action: {action}: {ex.GetType().Name} - {ex.Message}"
            );
            Console.WriteLine(ex.ToString());
            throw;
        }

        // 1. Player Action
        int newPlayerX = nextState.PlayerX;
        int newPlayerY = nextState.PlayerY;

        if (action == GameAction.MoveUp)
            newPlayerY--;
        else if (action == GameAction.MoveDown)
            newPlayerY++;
        else if (action == GameAction.MoveLeft)
            newPlayerX--;
        else if (action == GameAction.MoveRight)
            newPlayerX++;
        // GameAction.DoNothing means no change in position

        if (action != GameAction.DoNothing)
        {
            if (!nextState.IsValid(newPlayerX, newPlayerY))
            {
                // Player tried to move into a wall or out of bounds based on IsValid check.
                // Return current nextState, only tick and survival score are updated.
                return nextState;
            }
            nextState.PlayerX = newPlayerX;
            nextState.PlayerY = newPlayerY;
        }

        // 2. Handle consequences of player's new position
        int cellType;
        try
        {
            cellType = nextState.MapData[nextState.PlayerY, nextState.PlayerX];
        }
        catch (IndexOutOfRangeException finalIoex)
        {
            Console.WriteLine(
                "!!! IndexOutOfRangeException CAUGHT AT MapData[PlayerY, PlayerX] ACCESS!"
            );
            Console.WriteLine(
                $"Action: {action}, Tick: {nextState.CurrentTick}, Player attempting to access: ({nextState.PlayerX},{nextState.PlayerY})"
            );
            if (nextState.MapData != null)
            {
                Console.WriteLine(
                    $"MapData dimensions at time of error: {nextState.MapData.GetLength(0)}x{nextState.MapData.GetLength(1)}"
                );
            }
            else
            {
                Console.WriteLine("MapData was unexpectedly null at time of error.");
            }
            Console.WriteLine(finalIoex.ToString());
            throw; // Re-throw to maintain original behavior after logging
        }

        // 2a. Player moves ONTO Zookeeper's square?
        if (
            nextState.ZookeeperX != -1
            && // Zookeeper is active
            nextState.PlayerX == nextState.ZookeeperX
            && nextState.PlayerY == nextState.ZookeeperY
        )
        {
            nextState.Score -= 1000; // Penalty for being captured
            return nextState; // Terminal state, zookeeper does not move
        }

        // 2b. Player reaches an Escape Zone?
        if (nextState.MapData[nextState.PlayerY, nextState.PlayerX] == CellTypeEscapeZone)
        {
            nextState.Score += 1000; // Bonus for escaping
            // No change to MapData for escape zone, it remains an escape zone
            return nextState; // Terminal state, zookeeper does not move
        }

        // 2c. Player collects a Pellet? (Only if not captured or escaped)
        if (cellType == CellTypePellet)
        {
            nextState.Score += 10; // Reward for pellet
            nextState.MapData[nextState.PlayerY, nextState.PlayerX] = CellTypeEmpty; // Pellet is gone
        }

        // 3. Zookeeper Action (only if player was not captured and did not escape)
        if (nextState.ZookeeperX != -1 && nextState.ZookeeperY != -1)
        {
            // Simple: 1/5 chance to move towards player, 4/5 random valid move
            List<(int, int)> zkPossibleMoves = new List<(int, int)>();
            int[] dxActions = { 0, 0, 1, -1 }; // Corresponds to Right, Left, Down, Up for ZK logic
            int[] dyActions = { 1, -1, 0, 0 }; // But for simple moves: up, down, right, left

            // Corrected dx/dy for cardinal directions for zookeeper
            int[] zkDx = { 0, 0, 1, -1 }; // dx for {Up, Down, Right, Left}
            int[] zkDy = { -1, 1, 0, 0 }; // dy for {Up, Down, Right, Left}

            for (int i = 0; i < 4; ++i)
            {
                int nzx = nextState.ZookeeperX + zkDx[i];
                int nzy = nextState.ZookeeperY + zkDy[i];
                if (nextState.IsValid(nzx, nzy) && nextState.MapData[nzy, nzx] != CellTypeWall)
                {
                    zkPossibleMoves.Add((nzx, nzy));
                }
            }
            zkPossibleMoves.Add((nextState.ZookeeperX, nextState.ZookeeperY)); // Option to stay put

            if (zkPossibleMoves.Any())
            {
                // Basic chase logic (simplified): if random says chase, move towards player
                // This is a very naive chase, better would be pathfinding or smarter heuristic
                bool chasePlayer = random.Next(5) == 0; // 1/5 chance to chase
                (int chosenZKX, int chosenZKY) = zkPossibleMoves[
                    random.Next(zkPossibleMoves.Count)
                ]; // Default random

                if (chasePlayer)
                {
                    int bestDist = int.MaxValue;
                    (int, int) chaseMove = (nextState.ZookeeperX, nextState.ZookeeperY);
                    foreach (var move in zkPossibleMoves)
                    {
                        int dist =
                            Math.Abs(move.Item1 - nextState.PlayerX)
                            + Math.Abs(move.Item2 - nextState.PlayerY);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            chaseMove = move;
                        }
                    }
                    chosenZKX = chaseMove.Item1;
                    chosenZKY = chaseMove.Item2;
                }
                nextState.ZookeeperX = chosenZKX;
                nextState.ZookeeperY = chosenZKY;
            }

            // 3a. Zookeeper moves ONTO Player's square?
            if (
                nextState.PlayerX == nextState.ZookeeperX
                && nextState.PlayerY == nextState.ZookeeperY
            )
            {
                nextState.Score -= 1000; // Penalty for being captured
                // This state is now terminal
            }
        }

        return nextState;
    }

    public bool IsTerminal()
    {
        if (PlayerX == ZookeeperX && PlayerY == ZookeeperY && ZookeeperX != -1)
            return true; // Captured
        if (IsValid(PlayerX, PlayerY) && MapData[PlayerY, PlayerX] == CellTypeEscapeZone)
            return true; // Escaped
        if (CurrentTick >= MaxTicksForSimulation)
            return true; // Max ticks reached

        // Optional: if all pellets are collected (could be a win condition)
        // bool pelletsRemaining = false;
        // for (int i = 0; i < MapWidth; i++) for (int j = 0; j < MapHeight; j++) if (MapData[j,i] == CellTypePellet) pelletsRemaining = true;
        // if (!pelletsRemaining) return true;

        return false;
    }

    public GameResult GetGameResult()
    {
        if (!IsTerminal())
        {
            // This case should ideally not be hit if MCTS checks IsTerminal before calling GetGameResult
            // Or it means the simulation depth was cut short before a natural terminal state.
            // Return current score as an evaluation of a non-terminal state if simulation depth limit is hit.
            return new GameResult(Score);
        }

        // If terminal, ensure score reflects the terminal condition,
        // especially if the terminal state is the initial state and no ApplyMove has run.
        int terminalScore = Score;

        // Check for capture
        if (PlayerX == ZookeeperX && PlayerY == ZookeeperY && ZookeeperX != -1)
        {
            // If score doesn't already reflect capture penalty (e.g. from ApplyMove which adds +1 tick survival first)
            // The check (Score > -500) assumes that a heavily penalized score is already < -500.
            // If the score is 0 (initial state capture) and CurrentTick is 0, it should become -1000.
            // If ApplyMove set score to -999 (initialScore 0 + 1 tick - 1000 penalty), this block is skipped.
            if (Score > -500)
            {
                terminalScore = (CurrentTick > 0 ? Score - 1 : 0) - 1000; // Adjust if tick score was added
            }
        }
        // Check for escape
        else if (IsValid(PlayerX, PlayerY) && MapData[PlayerY, PlayerX] == CellTypeEscapeZone)
        {
            // Similar logic for escape bonus.
            // If ApplyMove set score to 1001 (initialScore 0 + 1 tick + 1000 bonus), this block is skipped.
            if (Score < 500)
            {
                terminalScore = (CurrentTick > 0 ? Score - 1 : 0) + 1000; // Adjust if tick score was added
            }
        }
        // If max ticks reached, the score is as-is from pellets/survival.

        return new GameResult(terminalScore);
    }
}
