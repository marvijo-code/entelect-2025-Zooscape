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
        private static readonly Random Rnd = new Random(Guid.NewGuid().GetHashCode());

        public ulong[] WallBitboard { get; private set; }
        public ulong[] PelletBitboard { get; private set; }

        // For simplicity, Zookeepers and the BotAnimal will be tracked by X,Y.
        // Bitboards for them can be added if many zookeepers or for specific proximity checks.
        public List<(int X, int Y)> ZookeeperPositions { get; private set; }
        public (int X, int Y) BotAnimalPosition { get; private set; }
        public int BotAnimalScore { get; private set; }
        public Guid BotAnimalId { get; private set; } // Keep for reference if needed
        public int PelletsCollected { get; private set; } // Track pellets collected in simulation
        public int ZookeeperAvoidances { get; private set; } // Track successful avoidances

        public FastGameState()
        {
            WallBitboard = new ulong[GameBoardSizing.UlongCount];
            PelletBitboard = new ulong[GameBoardSizing.UlongCount];
            ZookeeperPositions = new List<(int X, int Y)>();
            PelletsCollected = 0;
            ZookeeperAvoidances = 0;
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
            PelletsCollected = 0;
            ZookeeperAvoidances = 0;

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
                PelletsCollected = PelletsCollected,
                ZookeeperAvoidances = ZookeeperAvoidances,
            };
            Array.Copy(WallBitboard, clone.WallBitboard, WallBitboard.Length);
            Array.Copy(PelletBitboard, clone.PelletBitboard, PelletBitboard.Length);
            clone.ZookeeperPositions = new List<(int X, int Y)>(ZookeeperPositions); // Shallow copy of list, but tuples are value types
            return clone;
        }

        public FastGameState ApplyFast(Move move)
        {
            var nextState = Clone(); // Fast clone

            // First move the bot animal
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
                    nextState.PelletsCollected++; // Record pellet collection for reward
                    nextState.ClearBit(nextState.PelletBitboard, nextX, nextY);
                }
            }
            // If it's a wall, position doesn't change.

            // Now move the zookeepers - they chase the animal with some randomness
            var newZookeeperPositions = new List<(int X, int Y)>();
            foreach (var zkPos in nextState.ZookeeperPositions)
            {
                // Check if we're near the zookeeper (used for avoidance tracking)
                int distToZkBefore =
                    Math.Abs(nextState.BotAnimalPosition.X - zkPos.X)
                    + Math.Abs(nextState.BotAnimalPosition.Y - zkPos.Y);
                bool wasCloseToZk = distToZkBefore < 3;

                // 80% chance to move toward the animal, 20% to move randomly
                if (Rnd.NextDouble() < 0.8)
                {
                    // Basic pathfinding: Try to move in the direction of the bot animal
                    int zkNextX = zkPos.X;
                    int zkNextY = zkPos.Y;

                    // Horizontal movement preference
                    if (zkPos.X < nextState.BotAnimalPosition.X)
                        zkNextX++;
                    else if (zkPos.X > nextState.BotAnimalPosition.X)
                        zkNextX--;

                    // Wall check - if movement is blocked try vertical instead
                    if (nextState.IsSet(nextState.WallBitboard, zkNextX, zkPos.Y))
                    {
                        zkNextX = zkPos.X; // Reset to current X
                        // Vertical movement
                        if (zkPos.Y < nextState.BotAnimalPosition.Y)
                            zkNextY++;
                        else if (zkPos.Y > nextState.BotAnimalPosition.Y)
                            zkNextY--;
                    }

                    // Wall check for vertical - if still blocked, try alternate horizontal
                    if (nextState.IsSet(nextState.WallBitboard, zkPos.X, zkNextY))
                    {
                        zkNextY = zkPos.Y; // Reset to current Y
                        // Try opposite horizontal
                        if (zkPos.X < nextState.BotAnimalPosition.X)
                            zkNextX++;
                        else if (zkPos.X > nextState.BotAnimalPosition.X)
                            zkNextX--;
                    }

                    // If both attempts hit walls, zk stays in place
                    if (!nextState.IsSet(nextState.WallBitboard, zkNextX, zkNextY))
                    {
                        newZookeeperPositions.Add((zkNextX, zkNextY));
                    }
                    else
                    {
                        newZookeeperPositions.Add(zkPos); // Keep current position
                    }
                }
                else
                {
                    // Random movement
                    var zkMoves = new List<(int X, int Y)>();
                    // Check all four directions
                    if (!nextState.IsSet(nextState.WallBitboard, zkPos.X, zkPos.Y - 1))
                        zkMoves.Add((zkPos.X, zkPos.Y - 1)); // Up
                    if (!nextState.IsSet(nextState.WallBitboard, zkPos.X, zkPos.Y + 1))
                        zkMoves.Add((zkPos.X, zkPos.Y + 1)); // Down
                    if (!nextState.IsSet(nextState.WallBitboard, zkPos.X - 1, zkPos.Y))
                        zkMoves.Add((zkPos.X - 1, zkPos.Y)); // Left
                    if (!nextState.IsSet(nextState.WallBitboard, zkPos.X + 1, zkPos.Y))
                        zkMoves.Add((zkPos.X + 1, zkPos.Y)); // Right

                    if (zkMoves.Any())
                    {
                        // Choose random valid move
                        var randomIndex = Rnd.Next(zkMoves.Count);
                        newZookeeperPositions.Add(zkMoves[randomIndex]);
                    }
                    else
                    {
                        newZookeeperPositions.Add(zkPos); // No valid moves, stay in place
                    }
                }

                // Check if we successfully avoided the zookeeper
                int distToZkAfter =
                    Math.Abs(
                        nextState.BotAnimalPosition.X
                            - newZookeeperPositions[newZookeeperPositions.Count - 1].X
                    )
                    + Math.Abs(
                        nextState.BotAnimalPosition.Y
                            - newZookeeperPositions[newZookeeperPositions.Count - 1].Y
                    );

                // If we were close to a zookeeper and increased distance, count as avoidance
                if (wasCloseToZk && distToZkAfter > distToZkBefore)
                {
                    nextState.ZookeeperAvoidances++;
                }
            }

            nextState.ZookeeperPositions = newZookeeperPositions;
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

            // No pellets left - consider terminal to give higher reward
            bool noPelletsLeft = true;
            for (int i = 0; i < PelletBitboard.Length; ++i)
            {
                if (PelletBitboard[i] != 0)
                {
                    noPelletsLeft = false;
                    break;
                }
            }
            if (noPelletsLeft)
                return true;

            return false;
        }

        public double EvaluateFast(BotParameters parameters)
        {
            // Create more significant differentiation between good and bad moves
            double evalScore = 0.0;

            // Check for invalid position
            if (BotAnimalPosition.X == -1)
                return -100.0; // Much stronger negative to differentiate invalid states

            // Major factor: Pellets collected during simulation - strongly positive
            evalScore += PelletsCollected * 1.5;

            // Total score - existing accumulated score plus simulation gain
            evalScore += BotAnimalScore * 0.5;

            // Check if caught by zookeeper
            bool wasCaught = false;

            // Zookeeper evaluation - major factor with more dramatic penalties
            foreach (var zkPos in ZookeeperPositions)
            {
                // Caught by zookeeper - definite failure with strongly negative score
                if (BotAnimalPosition.X == zkPos.X && BotAnimalPosition.Y == zkPos.Y)
                {
                    wasCaught = true;
                    return -10.0; // Strongly negative but not so extreme it overwhelms all backpropagation
                }

                // Distance to zookeeper - graduated penalty based on proximity
                int distToZk =
                    Math.Abs(BotAnimalPosition.X - zkPos.X)
                    + Math.Abs(BotAnimalPosition.Y - zkPos.Y);

                // Scale avoidance penalties - much sharper gradient
                if (distToZk < 2)
                    evalScore -= 2.5; // Critical danger
                else if (distToZk < 3)
                    evalScore -= 1.5; // High danger
                else if (distToZk < 5)
                    evalScore -= 0.7; // Medium danger
                else if (distToZk < 7)
                    evalScore -= 0.3; // Low danger
            }

            // Reward for successfully avoiding zookeepers
            evalScore += ZookeeperAvoidances * 0.5;

            // Find closest pellet with higher reward for being close to pellets
            double minDistanceToPellet = double.MaxValue;
            bool pelletsExist = false;
            int pelletCount = 0;

            for (int y = 0; y < GameBoardSizing.MaxY; ++y)
            {
                for (int x = 0; x < GameBoardSizing.MaxX; ++x)
                {
                    if (IsSet(PelletBitboard, x, y))
                    {
                        pelletsExist = true;
                        pelletCount++;
                        int dist =
                            Math.Abs(BotAnimalPosition.X - x) + Math.Abs(BotAnimalPosition.Y - y);
                        if (dist < minDistanceToPellet)
                        {
                            minDistanceToPellet = dist;
                        }
                    }
                }
            }

            // Increased reward values for pellet proximity - more dramatic difference between distances
            if (pelletsExist && minDistanceToPellet > 0)
            {
                // More significant rewards for being closer to a pellet
                if (minDistanceToPellet <= 1)
                    evalScore += 2.0; // Adjacent to pellet - high reward
                else if (minDistanceToPellet <= 3)
                    evalScore += 1.0; // Close to pellet
                else if (minDistanceToPellet <= 6)
                    evalScore += 0.5; // Not too far from pellet
                else if (minDistanceToPellet <= 10)
                    evalScore += 0.2; // Within reasonable distance
                else
                    evalScore += 0.1; // Far but still heading to a pellet
            }
            else if (!pelletsExist && !wasCaught)
            {
                // Clearing all pellets is a win condition - major reward
                evalScore = 5.0; // Strongly positive
            }

            // Add a tiny bit of randomness to break ties (reduced from previous version)
            evalScore += (Rnd.NextDouble() - 0.5) * 0.005;

            // We don't normalize to [-1, 1] anymore to maintain meaningful differences
            return evalScore;
        }
    }
}
