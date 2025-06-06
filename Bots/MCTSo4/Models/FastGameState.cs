using System.Runtime.CompilerServices;
using MCTSo4.Enums; // For CellContent, Move

namespace MCTSo4.Models;

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

    // Direction vectors for quick access
    private static readonly (int dx, int dy)[] Directions =
    {
        (0, -1), // Up
        (0, 1), // Down
        (-1, 0), // Left
        (1, 0), // Right
    };

    // Expanded directions for area scanning (including diagonals)
    private static readonly (int dx, int dy)[] ExpandedDirections =
    {
        (0, -1),
        (1, -1),
        (1, 0),
        (1, 1),
        (0, 1),
        (-1, 1),
        (-1, 0),
        (-1, -1),
    };

    // For caching pellet counts in sectors
    private readonly int[] _pelletSectorCounts = new int[4]; // NE, SE, SW, NW
    private int _totalPelletsRemaining = -1;
    private bool _pelletCountsDirty = true;

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

    // Get the population count (number of bits set) in a bitboard - optimized with intrinsics
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PopCount(ulong[] board)
    {
        int count = 0;
        for (int i = 0; i < board.Length; i++)
        {
            count += System.Numerics.BitOperations.PopCount(board[i]);
        }
        return count;
    }

    // Direction-specific distance to closest pellet
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DirectionalDistanceToPellet(int dirIndex, int maxDist = 10)
    {
        var dir = Directions[dirIndex];
        var (x, y) = BotAnimalPosition;

        for (int d = 1; d <= maxDist; d++)
        {
            int nx = x + dir.dx * d;
            int ny = y + dir.dy * d;

            // Stop at walls
            if (IsSet(WallBitboard, nx, ny))
                return int.MaxValue;

            // Found a pellet
            if (IsSet(PelletBitboard, nx, ny))
                return d;
        }

        return maxDist + 1; // If no pellet found within range
    }

    // Count pellets in a specific sector relative to animal position
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdatePelletCounts()
    {
        if (!_pelletCountsDirty)
            return;

        Array.Clear(_pelletSectorCounts, 0, _pelletSectorCounts.Length);
        _totalPelletsRemaining = 0;
        var (animalX, animalY) = BotAnimalPosition;

        for (int y = 0; y < GameBoardSizing.MaxY; y++)
        {
            for (int x = 0; x < GameBoardSizing.MaxX; x++)
            {
                if (IsSet(PelletBitboard, x, y))
                {
                    _totalPelletsRemaining++;

                    // Determine sector (NE, SE, SW, NW)
                    int sectorIndex = ((y > animalY) ? 1 : 0) + ((x < animalX) ? 2 : 0);
                    _pelletSectorCounts[sectorIndex]++;
                }
            }
        }

        _pelletCountsDirty = false;
    }

    // Calculates pellet density score in a direction
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double PelletDensityScore(int dirIndex, int maxDist = 8)
    {
        double score = 0;
        var dir = Directions[dirIndex];
        var (x, y) = BotAnimalPosition;

        for (int d = 1; d <= maxDist; d++)
        {
            int nx = x + dir.dx * d;
            int ny = y + dir.dy * d;

            // Stop at walls
            if (IsSet(WallBitboard, nx, ny))
                break;

            // Add weighted score for pellets (closer ones are worth more)
            if (IsSet(PelletBitboard, nx, ny))
            {
                score += 1.0 / d;
            }
        }

        return score;
    }

    // Calculate danger level from zookeepers in a specific direction
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double DirectionalZookeeperDanger(int dirIndex, int maxDist = 8)
    {
        var dir = Directions[dirIndex];
        var (x, y) = BotAnimalPosition;
        double danger = 0;

        foreach (var zkPos in ZookeeperPositions)
        {
            // Skip if the zookeeper is in a completely different direction
            int dxToZk = zkPos.X - x;
            int dyToZk = zkPos.Y - y;

            // Check if zookeeper is roughly in this direction
            bool sameDirection = false;

            if (dir.dx != 0 && Math.Sign(dxToZk) == Math.Sign(dir.dx))
                sameDirection = true;

            if (dir.dy != 0 && Math.Sign(dyToZk) == Math.Sign(dir.dy))
                sameDirection = true;

            if (sameDirection)
            {
                int dist = Math.Abs(x - zkPos.X) + Math.Abs(y - zkPos.Y);
                if (dist <= maxDist)
                {
                    danger += Math.Max(0, 10.0 - dist) / 10.0; // More danger when closer
                }
            }
        }

        return danger;
    }

    // Generates a safety map for quick zookeeper threat assessment
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double[,] GenerateSafetyMap(int radius = 8)
    {
        double[,] safetyMap = new double[2 * radius + 1, 2 * radius + 1];
        var (x, y) = BotAnimalPosition;

        // Add danger zones around zookeepers
        foreach (var zkPos in ZookeeperPositions)
        {
            int zkDx = zkPos.X - x;
            int zkDy = zkPos.Y - y;

            // Only consider zookeepers in range
            if (Math.Abs(zkDx) <= radius && Math.Abs(zkDy) <= radius)
            {
                // Mark danger around the zookeeper with decreasing intensity
                for (int dy = -3; dy <= 3; dy++)
                {
                    for (int dx = -3; dx <= 3; dx++)
                    {
                        int mapX = radius + zkDx + dx;
                        int mapY = radius + zkDy + dy;

                        // Check map bounds
                        if (
                            mapX >= 0
                            && mapX < 2 * radius + 1
                            && mapY >= 0
                            && mapY < 2 * radius + 1
                        )
                        {
                            // Calculate distance from the zookeeper
                            int dist = Math.Abs(dx) + Math.Abs(dy);
                            if (dist <= 3)
                            {
                                // Higher danger closer to zookeeper
                                double danger = 1.0 - (dist / 4.0);
                                safetyMap[mapX, mapY] = Math.Max(safetyMap[mapX, mapY], danger);
                            }
                        }
                    }
                }
            }
        }

        return safetyMap;
    }

    public void InitializeFromMCTSGameState(MCTSGameState source, Guid botId)
    {
        BotAnimalId = botId;
        Array.Clear(WallBitboard, 0, WallBitboard.Length);
        Array.Clear(PelletBitboard, 0, PelletBitboard.Length);
        ZookeeperPositions.Clear();
        PelletsCollected = 0;
        ZookeeperAvoidances = 0;
        _pelletCountsDirty = true;

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
            _pelletCountsDirty = true, // Force recalculation in the clone
        };
        Array.Copy(WallBitboard, clone.WallBitboard, WallBitboard.Length);
        Array.Copy(PelletBitboard, clone.PelletBitboard, PelletBitboard.Length);
        clone.ZookeeperPositions = new List<(int X, int Y)>(ZookeeperPositions); // Shallow copy of list, but tuples are value types
        return clone;
    }

    public FastGameState ApplyFast(Move move)
    {
        var nextState = Clone(); // Fast clone
        nextState._pelletCountsDirty = true; // Mark dirty for the new state

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
        // Create more significant differentiation between moves
        double evalScore = 0.0;

        // Check for invalid position
        if (BotAnimalPosition.X == -1)
            return -100.0; // Much stronger negative to differentiate invalid states

        // Ensure pellet counts are updated
        UpdatePelletCounts();

        // ---------- PELLET COLLECTION REWARDS ----------

        // Major factor: Pellets collected during simulation - strongly positive
        evalScore += PelletsCollected * 15.0;

        // Total score - existing accumulated score plus simulation gain
        evalScore += BotAnimalScore * 5.0;

        // ---------- ZOOKEEPER AVOIDANCE EVALUATION ----------
        bool wasCaught = false;
        double zookeeperDangerScore = 0;

        // Generate safety map for more precise danger zones
        var safetyMap = GenerateSafetyMap(8);
        double centerDanger = safetyMap[8, 8]; // Current position danger

        // Zookeeper evaluation - major factor with more dramatic penalties
        foreach (var zkPos in ZookeeperPositions)
        {
            // Caught by zookeeper - definite failure with strongly negative score
            if (BotAnimalPosition.X == zkPos.X && BotAnimalPosition.Y == zkPos.Y)
            {
                wasCaught = true;
                return -100.0; // Strongly negative but not so extreme it overwhelms all backpropagation
            }

            // Distance to zookeeper - graduated penalty based on proximity
            int distToZk =
                Math.Abs(BotAnimalPosition.X - zkPos.X) + Math.Abs(BotAnimalPosition.Y - zkPos.Y);

            // Scale avoidance penalties - much sharper gradient
            if (distToZk < 2)
                zookeeperDangerScore -= 25.0; // Critical danger
            else if (distToZk < 3)
                zookeeperDangerScore -= 15.0; // High danger
            else if (distToZk < 5)
                zookeeperDangerScore -= 7.0; // Medium danger
            else if (distToZk < 7)
                zookeeperDangerScore -= 3.0; // Low danger
        }

        evalScore += zookeeperDangerScore;

        // Reward for successfully avoiding zookeepers
        evalScore += ZookeeperAvoidances * 5.0;

        // ---------- DIRECTIONAL EVALUATION ----------

        // Evaluate all four directions for pellet collection potential
        double[] directionScores = new double[4];
        for (int dir = 0; dir < 4; dir++)
        {
            // Calculate distance to nearest pellet in this direction
            int pelletDist = DirectionalDistanceToPellet(dir);

            // Calculate density of pellets in this direction
            double pelletDensity = PelletDensityScore(dir);

            // Calculate danger from zookeepers in this direction
            double zkDanger = DirectionalZookeeperDanger(dir);

            // Combined directional score
            directionScores[dir] =
                (pelletDist < int.MaxValue ? 10.0 / (pelletDist + 1) : 0)
                + // Reward for pellet proximity
                pelletDensity * 20.0
                - // Reward for pellet density
                zkDanger * 30.0; // Penalty for zookeeper danger
        }

        // Determine the best direction based on scores
        int bestDirIndex = 0;
        for (int i = 1; i < 4; i++)
        {
            if (directionScores[i] > directionScores[bestDirIndex])
                bestDirIndex = i;
        }

        // Add bonus based on the best direction score
        evalScore += directionScores[bestDirIndex] * 1.5;

        // ---------- PELLET SECTOR ANALYSIS ----------

        // Determine which sector has most pellets
        int bestSectorIndex = 0;
        for (int i = 1; i < 4; i++)
        {
            if (_pelletSectorCounts[i] > _pelletSectorCounts[bestSectorIndex])
                bestSectorIndex = i;
        }

        // Get direction to the best sector
        int xDir = (bestSectorIndex & 2) != 0 ? -1 : 1; // Left for sectors 2,3; Right for 0,1
        int yDir = (bestSectorIndex & 1) != 0 ? 1 : -1; // Down for sectors 1,3; Up for 0,2

        // Add bonus for moving toward the sector with most pellets
        // Map sector directions to moves: 0=NE, 1=SE, 2=SW, 3=NW
        Move[] sectorMoves = { Move.Right, Move.Down, Move.Left, Move.Up };

        // ---------- WIN/LOSS CONDITIONS ----------

        if (wasCaught)
        {
            return -100.0; // Strong negative
        }
        else if (_totalPelletsRemaining == 0)
        {
            // Clearing all pellets is a win condition - major reward
            return 100.0; // Strongly positive
        }

        // Add a tiny bit of randomness to break ties (reduced from previous version)
        evalScore += (Rnd.NextDouble() - 0.5) * 0.5;

        // Return the total score - much more differentiated now
        return evalScore;
    }
}
