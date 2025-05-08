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
        MCTSGameState nextState = this.Clone();
        nextState.CurrentTick++;
        nextState.Score++; // +1 for surviving a tick

        // 1. Player Action
        int nextPlayerX = nextState.PlayerX;
        int nextPlayerY = nextState.PlayerY;

        switch (action)
        {
            case GameAction.MoveUp:
                nextPlayerY--;
                break;
            case GameAction.MoveDown:
                nextPlayerY++;
                break;
            case GameAction.MoveLeft:
                nextPlayerX--;
                break;
            case GameAction.MoveRight:
                nextPlayerX++;
                break;
            case GameAction.DoNothing:
                break;
        }

        if (
            nextState.IsValid(nextPlayerX, nextPlayerY)
            && nextState.MapData[nextPlayerY, nextPlayerX] != CellTypeWall
        )
        {
            nextState.PlayerX = nextPlayerX;
            nextState.PlayerY = nextPlayerY;
        }
        // else: bumped into wall or invalid move, player stays put (already handled by GetPossibleMoves pre-check)

        // Check consequences of player's new position
        int cellTypeAtPlayerPos = nextState.MapData[nextState.PlayerY, nextState.PlayerX];
        if (cellTypeAtPlayerPos == CellTypePellet)
        {
            nextState.Score += 10;
            nextState.MapData[nextState.PlayerY, nextState.PlayerX] = CellTypeEmpty; // Pellet consumed
        }
        else if (cellTypeAtPlayerPos == CellTypePowerUp)
        {
            nextState.Score += 5; // Placeholder for power-up effect
            nextState.MapData[nextState.PlayerY, nextState.PlayerX] = CellTypeEmpty; // Power-up consumed
            // TODO: Implement actual power-up effect, e.g., temporary invincibility
        }
        else if (cellTypeAtPlayerPos == CellTypeEscapeZone)
        {
            nextState.Score += 1000; // Reached escape zone - strong positive score
            // This state will be marked terminal
        }

        // 2. Zookeeper Action (simple random move or basic chase for simulation)
        if (nextState.ZookeeperX != -1 && nextState.ZookeeperY != -1)
        {
            // Simple: 1/5 chance to move towards player, 4/5 random valid move
            List<(int, int)> zkPossibleMoves = new List<(int, int)>();
            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            for (int i = 0; i < 4; ++i)
            {
                int nzx = nextState.ZookeeperX + dx[i];
                int nzy = nextState.ZookeeperY + dy[i];
                if (nextState.IsValid(nzx, nzy) && nextState.MapData[nzy, nzx] != CellTypeWall)
                {
                    zkPossibleMoves.Add((nzx, nzy));
                }
            }
            zkPossibleMoves.Add((nextState.ZookeeperX, nextState.ZookeeperY)); // Option to stay put

            if (zkPossibleMoves.Any())
            {
                (int chosenZKX, int chosenZKY) = zkPossibleMoves[
                    random.Next(zkPossibleMoves.Count)
                ];
                nextState.ZookeeperX = chosenZKX;
                nextState.ZookeeperY = chosenZKY;
            }
        }

        // Check for capture after both have moved
        if (
            nextState.PlayerX == nextState.ZookeeperX
            && nextState.PlayerY == nextState.ZookeeperY
            && ZookeeperX != -1
        ) // Added ZK active check
        {
            nextState.Score -= 1000; // Captured by zookeeper - strong penalty
            // This state will be marked terminal
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
            // If score doesn't already reflect capture penalty (e.g. from ApplyMove)
            // This check is a bit naive, as Score could be -1000 for other reasons or compound.
            // A more robust way might involve flags or ensuring ApplyMove is the sole source of these large score changes.
            if (Score > -500) // Assuming score isn't already heavily penalized
            {
                terminalScore = (CurrentTick > 0 ? Score : 0) - 1000; // Base on current score if ticks happened, else 0
            }
        }
        // Check for escape
        else if (IsValid(PlayerX, PlayerY) && MapData[PlayerY, PlayerX] == CellTypeEscapeZone)
        {
            // If score doesn't already reflect escape bonus
            if (Score < 500) // Assuming score isn't already heavily rewarded
            {
                terminalScore = (CurrentTick > 0 ? Score : 0) + 1000;
            }
        }
        // If max ticks reached, the score is as-is from pellets/survival, no special adjustment here needed
        // beyond what ApplyMove has done.

        return new GameResult(terminalScore);
    }
}
