using System;
using System.Collections.Generic;
using System.Linq;
using MCTSo4.Enums; // For CellContent, Move

namespace MCTSo4.Models
{
    // Represents the game board dimensions
    // TODO: Make these configurable or derive from actual game init if possible
    public static class GameBoardSizing
    {
        public const int MaxX = 50; // Based on observed game states
        public const int MaxY = 50;
        public const int BoardSize = MaxX * MaxY;
        public const int UlongCount = (BoardSize + 63) / 64; // Number of ulongs needed
    }

    public class FastGameState
    {
        public ulong[] WallBitboard { get; private set; }
        public ulong[] PelletBitboard { get; private set; }

        // For simplicity, Zookeepers and the BotAnimal will be tracked by X,Y.
        // Bitboards for them can be added if many zookeepers or for specific proximity checks.
        public List<(int X, int Y)> ZookeeperPositions { get; private set; }
        public (int X, int Y) BotAnimalPosition { get; private set; }
        public int BotAnimalScore { get; private set; }
        public Guid BotAnimalId { get; private set; } // Keep for reference if needed

        public FastGameState()
        {
            WallBitboard = new ulong[GameBoardSizing.UlongCount];
            PelletBitboard = new ulong[GameBoardSizing.UlongCount];
            ZookeeperPositions = new List<(int X, int Y)>();
        }

        // Helper to set a bit for a given X,Y coordinate
        private void SetBit(ulong[] board, int x, int y)
        {
            if (x < 0 || x >= GameBoardSizing.MaxX || y < 0 || y >= GameBoardSizing.MaxY)
                return; // Bounds check
            int bitIndex = y * GameBoardSizing.MaxX + x;
            board[bitIndex / 64] |= (1UL << (bitIndex % 64));
        }

        // Helper to clear a bit
        private void ClearBit(ulong[] board, int x, int y)
        {
            if (x < 0 || x >= GameBoardSizing.MaxX || y < 0 || y >= GameBoardSizing.MaxY)
                return;
            int bitIndex = y * GameBoardSizing.MaxX + x;
            board[bitIndex / 64] &= ~(1UL << (bitIndex % 64));
        }

        // Helper to check a bit
        private bool IsSet(ulong[] board, int x, int y)
        {
            if (x < 0 || x >= GameBoardSizing.MaxX || y < 0 || y >= GameBoardSizing.MaxY)
                return true; // Treat out of bounds as a wall for safety
            int bitIndex = y * GameBoardSizing.MaxX + x;
            return (board[bitIndex / 64] & (1UL << (bitIndex % 64))) != 0;
        }

        public void InitializeFromMCTSGameState(MCTSGameState source, Guid botId)
        {
            BotAnimalId = botId;
            Array.Clear(WallBitboard, 0, WallBitboard.Length);
            Array.Clear(PelletBitboard, 0, PelletBitboard.Length);
            ZookeeperPositions.Clear();

            foreach (var cell in source.Cells)
            {
                if (cell.Content == CellContent.Wall)
                    SetBit(WallBitboard, cell.X, cell.Y);
                else if (cell.Content == CellContent.Pellet)
                    SetBit(PelletBitboard, cell.X, cell.Y);
            }

            var animal = source.Animals.FirstOrDefault(a => a.Id == botId);
            if (animal != null)
            {
                BotAnimalPosition = (animal.X, animal.Y);
                BotAnimalScore = animal.Score;
            }
            else
            {
                // This case should ideally be handled before calling (e.g. if botId is Guid.Empty)
                // Or set to a default/invalid state
                BotAnimalPosition = (-1, -1);
                BotAnimalScore = 0;
            }

            foreach (var zk in source.Zookeepers)
            {
                ZookeeperPositions.Add((zk.X, zk.Y));
            }
        }

        public FastGameState Clone()
        {
            var clone = new FastGameState
            {
                BotAnimalPosition = BotAnimalPosition,
                BotAnimalScore = BotAnimalScore,
                BotAnimalId = BotAnimalId,
            };
            Array.Copy(WallBitboard, clone.WallBitboard, WallBitboard.Length);
            Array.Copy(PelletBitboard, clone.PelletBitboard, PelletBitboard.Length);
            clone.ZookeeperPositions = new List<(int X, int Y)>(ZookeeperPositions); // Shallow copy of list, but tuples are value types
            return clone;
        }

        public FastGameState ApplyFast(Move move)
        {
            var nextState = Clone(); // Fast clone

            (int currentX, int currentY) = nextState.BotAnimalPosition;
            int nextX = currentX;
            int nextY = currentY;

            switch (move)
            {
                case Move.Up:
                    nextY--;
                    break;
                case Move.Down:
                    nextY++;
                    break;
                case Move.Left:
                    nextX--;
                    break;
                case Move.Right:
                    nextX++;
                    break;
            }

            if (!nextState.IsSet(nextState.WallBitboard, nextX, nextY)) // Not a wall
            {
                nextState.BotAnimalPosition = (nextX, nextY);
                if (nextState.IsSet(nextState.PelletBitboard, nextX, nextY))
                {
                    nextState.BotAnimalScore++;
                    nextState.ClearBit(nextState.PelletBitboard, nextX, nextY);
                }
            }
            // If it's a wall, position doesn't change.
            return nextState;
        }

        public List<Move> GetLegalMovesFast()
        {
            var legalMoves = new List<Move>();
            (int currentX, int currentY) = BotAnimalPosition;

            // Check Up
            if (!IsSet(WallBitboard, currentX, currentY - 1))
                legalMoves.Add(Move.Up);
            // Check Down
            if (!IsSet(WallBitboard, currentX, currentY + 1))
                legalMoves.Add(Move.Down);
            // Check Left
            if (!IsSet(WallBitboard, currentX - 1, currentY))
                legalMoves.Add(Move.Left);
            // Check Right
            if (!IsSet(WallBitboard, currentX + 1, currentY))
                legalMoves.Add(Move.Right);

            return legalMoves;
        }

        public bool IsTerminalFast(int currentTickInSim, int maxSimDepth, BotParameters parameters) // Added parameters for potential use
        {
            // Caught by Zookeeper
            foreach (var zkPos in ZookeeperPositions) // Using the ZookeeperPositions from FastGameState
            {
                if (BotAnimalPosition.X == zkPos.X && BotAnimalPosition.Y == zkPos.Y)
                {
                    return true;
                }
            }

            // Simulation depth reached
            if (currentTickInSim >= maxSimDepth)
                return true;

            // Optional: No pellets left? (Can be slow to check all bits)
            // bool noPelletsLeft = true;
            // for(int i=0; i<PelletBitboard.Length; ++i) { if(PelletBitboard[i] != 0) {noPelletsLeft=false; break;} }
            // if(noPelletsLeft) return true;

            return false;
        }

        public double EvaluateFast(BotParameters parameters) // Using BotParameters passed in
        {
            if (BotAnimalPosition.X == -1)
                return -200000; // Bot animal not found during init, very bad state

            double evalScore = 0.0;
            evalScore += BotAnimalScore * parameters.Weight_PelletValue;
            bool wasCaught = false;

            foreach (var zkPos in ZookeeperPositions)
            {
                if (BotAnimalPosition.X == zkPos.X && BotAnimalPosition.Y == zkPos.Y)
                {
                    evalScore -= 100000;
                    wasCaught = true;
                    break;
                }
                int distToZk =
                    Math.Abs(BotAnimalPosition.X - zkPos.X)
                    + Math.Abs(BotAnimalPosition.Y - zkPos.Y);
                if (distToZk < 5 && distToZk > 0)
                {
                    evalScore += parameters.Weight_ZkThreat / distToZk;
                }
            }

            // Pellet attraction: find closest pellet (can be slow, consider alternatives for pure bitboard)
            // For now, this iterates through the board to find set bits if needed.
            // A simpler heuristic might be just total pellet count or pellet density in player's quadrant.
            double minDistanceToPellet = double.MaxValue;
            bool pelletsExist = false;
            for (int y = 0; y < GameBoardSizing.MaxY; ++y)
            {
                for (int x = 0; x < GameBoardSizing.MaxX; ++x)
                {
                    if (IsSet(PelletBitboard, x, y))
                    {
                        pelletsExist = true;
                        int dist =
                            Math.Abs(BotAnimalPosition.X - x) + Math.Abs(BotAnimalPosition.Y - y);
                        if (dist < minDistanceToPellet)
                        {
                            minDistanceToPellet = dist;
                        }
                    }
                }
            }

            if (pelletsExist && minDistanceToPellet > 0)
            {
                evalScore += (parameters.Weight_PelletValue * 2) / (minDistanceToPellet + 1);
            }
            else if (!pelletsExist && !wasCaught)
            {
                evalScore += 5000; // No pellets left and not caught bonus
            }
            return evalScore;
        }
    }
}
