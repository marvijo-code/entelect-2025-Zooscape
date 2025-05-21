using System;
using System.Collections.Generic; // Added for List and Queue
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using DeepMCTS.Enums; // Changed namespace
using DeepMCTS.Models; // Changed namespace

// Assuming GameState, BotCommand, BotAction, CellContent are defined in MCTSo4.Models or DeepMCTS.Models
// Adjust namespaces as necessary after project setup
namespace DeepMCTS.Services;

public class BotService
{
    private Guid _myBotId; // Added to store the bot's official ID from the server

    // Define time limits and buffer
    private const int TARGET_TOTAL_RESPONSE_TIME_MS = 150; // Target for the entire ProcessState
    private const int PROCESSING_OVERHEAD_BUFFER_MS = 20; // Buffer for non-MCTS work and network latency
    private const int MCTS_TIME_LIMIT_MS =
        TARGET_TOTAL_RESPONSE_TIME_MS - PROCESSING_OVERHEAD_BUFFER_MS;

    // Method to set the bot's official ID when received from the server
    public void SetOfficialBotId(Guid botId)
    {
        _myBotId = botId;
        // Potentially log here: Console.WriteLine($"BotService: Official Bot ID set to {_myBotId}");
    }

    // Getter for _myBotId
    public Guid GetMyBotId()
    {
        return _myBotId;
    }

    public BotCommand ProcessState(GameState state)
    {
        var stopwatch = Stopwatch.StartNew(); // Start stopwatch

        // Ensure MyAnimalId is set in GameState for MonteCarlo to use state.MyAnimal
        // If state.MyAnimalId is not set by the deserializer, we can use _myBotId here.
        if (state.MyAnimalId == Guid.Empty && _myBotId != Guid.Empty)
        {
            state.MyAnimalId = _myBotId;
        }
        // It's crucial that state.MyAnimalId is correctly populated before InitGameState if it relies on it.

        MonteCarlo.InitGameState(state);
        var mctsResult = MonteCarlo.FindBestMove(state, timeLimitMs: MCTS_TIME_LIMIT_MS);

        stopwatch.Stop(); // Stop stopwatch

        Console.WriteLine(
            $"[DeepMCTS Stats] Tick: {state.Tick}, TargetLimit: {MCTS_TIME_LIMIT_MS}ms, ActualDuration: {stopwatch.ElapsedMilliseconds}ms, Iterations: {mctsResult.Iterations}, SimDepth: {mctsResult.SimulationDepth}, ChosenAction: {mctsResult.Action}"
        );

        // Optional: Check if we still went over the TARGET_TOTAL_RESPONSE_TIME_MS
        if (stopwatch.ElapsedMilliseconds > TARGET_TOTAL_RESPONSE_TIME_MS)
        {
            Console.WriteLine(
                $"[DeepMCTS Warning] Tick: {state.Tick} - Total processing time {stopwatch.ElapsedMilliseconds}ms exceeded target of {TARGET_TOTAL_RESPONSE_TIME_MS}ms."
            );
        }

        return new BotCommand { Action = mctsResult.Action };
    }
}

// Define a struct for the return type of FindBestMove
public struct MctsResult
{
    public BotAction Action { get; set; }
    public int Iterations { get; set; }
    public int SimulationDepth { get; set; }
}

static class MonteCarlo
{
    // Static precomputed game data (initialized once)
    static int width,
        height;
    static bool[,]? walls; // Made nullable
    static ulong[]? pelletInitial; // Made nullable
    static int botSpawnX,
        botSpawnY;
    static int zooSpawnX,
        zooSpawnY;
    static Random rng = new Random();
    const int PELLET_POINTS = 10; // Points per pellet (game config)
    const int RETARGET_INTERVAL = 20; // Zookeeper recalculation interval
    public static int MaxSimulationTicks { get; set; } = 50; // Changed from const to a public static property with a default value

    public static void InitGameState(GameState state)
    {
        if (state.MyAnimal == null)
        {
            // Log or throw an exception: MyAnimal is essential.
            // This can happen if MyAnimalId is not set correctly in GameState
            // or if no animal with that ID exists in the Animals list.
            Console.WriteLine(
                "Error: state.MyAnimal is null in InitGameState. Ensure MyAnimalId is correctly set in GameState."
            );
            // Depending on error handling strategy, you might throw or return.
            // For now, let it proceed, but 'walls' might remain null if width/height can't be determined.
            // However, state.Cells.Max would fail.
            return;
        }

        if (walls == null)
        {
            // Compute grid size
            // Check if Cells is null or empty before calling Max
            if (state.Cells == null || !state.Cells.Any())
            {
                Console.WriteLine("Error: state.Cells is null or empty in InitGameState.");
                // Cannot initialize walls or pelletInitial without cell data.
                return;
            }
            width = state.Cells.Max(c => c.X) + 1;
            height = state.Cells.Max(c => c.Y) + 1;
            walls = new bool[width, height]; // Now initialized

            int totalCells = width * height;
            pelletInitial = new ulong[(totalCells + 63) / 64]; // Now initialized

            foreach (var cell in state.Cells)
            {
                if (cell.Content == CellContent.Wall)
                {
                    walls[cell.X, cell.Y] = true;
                }
                else if (cell.Content == CellContent.Pellet)
                {
                    int idx = cell.Y * width + cell.X;
                    pelletInitial[idx >> 6] |= 1UL << (idx & 63);
                }
                // Ensure state.MyAnimal is not null before accessing its properties
                else if (
                    cell.Content == CellContent.AnimalSpawn
                    && state.MyAnimal != null
                    && cell.X == state.MyAnimal.X
                    && cell.Y == state.MyAnimal.Y
                )
                {
                    botSpawnX = cell.X;
                    botSpawnY = cell.Y;
                }
                else if (cell.Content == CellContent.ZookeeperSpawn)
                {
                    zooSpawnX = cell.X;
                    zooSpawnY = cell.Y;
                }
            }
        }
    }

    public static MctsResult FindBestMove(GameState state, int timeLimitMs)
    {
        if (walls == null || pelletInitial == null)
        {
            Console.WriteLine("Error: Walls or pelletInitial not initialized in FindBestMove.");
            return new MctsResult
            {
                Action = BotAction.Up,
                Iterations = 0,
                SimulationDepth = MaxSimulationTicks,
            };
        }
        if (state.MyAnimal == null)
        {
            Console.WriteLine("Error: state.MyAnimal is null in FindBestMove.");
            return new MctsResult
            {
                Action = BotAction.Up,
                Iterations = 0,
                SimulationDepth = MaxSimulationTicks,
            };
        }
        if (state.Zookeepers == null || !state.Zookeepers.Any())
        {
            Console.WriteLine(
                "Warning: state.Zookeepers is null or empty. Simulating without zookeepers."
            );
            return new MctsResult
            {
                Action = BotAction.Up,
                Iterations = 0,
                SimulationDepth = MaxSimulationTicks,
            };
        }

        ulong[] pelletCurrent = new ulong[pelletInitial.Length];
        pelletInitial.CopyTo(pelletCurrent, 0);

        if (state.Cells != null) // Add null check for state.Cells
        {
            foreach (var cell in state.Cells.Where(c => c.Content == CellContent.Empty))
            {
                int idx = cell.Y * width + cell.X;
                pelletCurrent[idx >> 6] &= ~(1UL << (idx & 63));
            }
        }

        var myBot = state.MyAnimal; // Already checked for null
        int startX = myBot.X,
            startY = myBot.Y;
        // Assuming at least one zookeeper. state.Zookeepers null/empty check done above.
        int zooX = state.Zookeepers[0].X,
            zooY = state.Zookeepers[0].Y;

        int[] totalScore = new int[4];
        int iterations = 0;
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < timeLimitMs)
        {
            for (int action = 0; action < 4; action++)
            {
                // Skip illegal immediate moves (e.g., into wall)
                // Added null check for walls here as well for safety, though it should be initialized.
                if (walls == null)
                    break; // Should not happen

                bool isIllegal = false;
                switch (action)
                {
                    case 0: // Up
                        isIllegal = (startY == 0 || (startY > 0 && walls[startX, startY - 1]));
                        break;
                    case 1: // Down
                        isIllegal = (
                            startY == height - 1
                            || (startY < height - 1 && walls[startX, startY + 1])
                        );
                        break;
                    case 2: // Left
                        isIllegal = (startX == 0 || (startX > 0 && walls[startX - 1, startY]));
                        break;
                    case 3: // Right
                        isIllegal = (
                            startX == width - 1 || (startX < width - 1 && walls[startX + 1, startY])
                        );
                        break;
                }

                if (isIllegal)
                {
                    continue;
                }
                int score = SimulatePlayout(startX, startY, zooX, zooY, action, pelletCurrent);
                totalScore[action] += score;
            }
            if (walls == null)
                break; // Check again in case it became null (highly unlikely)
            iterations++;
        }

        int bestActionInt = 0;
        double bestAvg = double.NegativeInfinity;
        for (int a = 0; a < 4; a++)
        {
            double avg =
                (iterations == 0) ? double.NegativeInfinity : (double)totalScore[a] / iterations;
            if (avg > bestAvg)
            {
                bestAvg = avg;
                bestActionInt = a;
            }
        }
        return new MctsResult
        {
            Action = (BotAction)(bestActionInt + 1),
            Iterations = iterations,
            SimulationDepth = MaxSimulationTicks,
        };
    }

    private static int SimulatePlayout(
        int botX,
        int botY,
        int zooX,
        int zooY,
        int initialAction,
        ulong[] pelletMap
    )
    {
        if (walls == null) // Ensure walls is not null
        {
            Console.WriteLine("Error: walls not initialized in SimulatePlayout");
            return 0; // Or throw
        }

        ulong[] pelletBits = new ulong[pelletMap.Length];
        pelletMap.CopyTo(pelletBits, 0);
        int score = 0;
        bool botInCage = (botX == botSpawnX && botY == botSpawnY);

        ApplyMove(initialAction, ref botX, ref botY);
        ConsumePellet(botX, botY, pelletBits, ref score);
        if (botInCage && (botX != botSpawnX || botY != botSpawnY))
        {
            botInCage = false;
        }

        int ticksUntilRecalc = RETARGET_INTERVAL;
        int dirToTargetX = 0,
            dirToTargetY = 0;
        if (!botInCage)
        {
            ComputeGhostDirection(botX, botY, zooX, zooY, out dirToTargetX, out dirToTargetY);
        }

        // Using MaxSimulationTicks from class level
        int lastMove = initialAction;
        for (int t = 1; t < MaxSimulationTicks; t++) // Use the class-level constant
        {
            int action;
            if (rng.NextDouble() < 0.7)
            {
                action = rng.Next(0, 4);
                if (
                    (
                        (lastMove == 0 && action == 1)
                        || (lastMove == 1 && action == 0)
                        || (lastMove == 2 && action == 3)
                        || (lastMove == 3 && action == 2)
                    ) && CanMove(botX, botY, lastMove)
                )
                {
                    action = lastMove;
                }
                else if (
                    (
                        (lastMove == 0 && action == 1)
                        || (lastMove == 1 && action == 0)
                        || (lastMove == 2 && action == 3)
                        || (lastMove == 3 && action == 2)
                    ) && !CanMove(botX, botY, lastMove)
                )
                {
                    int newAction;
                    do
                    {
                        newAction = rng.Next(0, 4);
                    } while (newAction == action);
                    action = newAction;
                }
            }
            else
            {
                action = rng.Next(0, 4);
            }

            int prevBotX = botX;
            int prevBotY = botY;
            ApplyMove(action, ref botX, ref botY);

            if (botX != prevBotX || botY != prevBotY)
            {
                ConsumePellet(botX, botY, pelletBits, ref score);
                botInCage = false;
            }
            lastMove = action;

            if (!botInCage)
            {
                int prevZooX = zooX;
                int prevZooY = zooY;

                if (dirToTargetX != 0 || dirToTargetY != 0)
                {
                    int nextZooX = zooX + dirToTargetX;
                    int nextZooY = zooY + dirToTargetY; // Original logic tried to move based on dirToTargetY too

                    // Zookeeper tries to move in X direction first if primary direction
                    if (dirToTargetX != 0)
                    {
                        if (nextZooX >= 0 && nextZooX < width && !walls[nextZooX, zooY])
                        {
                            zooX = nextZooX;
                        }
                    }
                    // Then tries Y if primary or X was blocked/not primary
                    // This logic means if dirX !=0 and dirY !=0, it could move in X, then try Y in same tick if BFS gave diagonal.
                    // A stricter BFS would only give one cardinal step.
                    // If dirToTargetX moved zooX, and dirToTargetY is also non-zero, we should check if it can move in Y from new zooX.
                    // For now, let's stick to: if X is primary, move X. If Y is primary (dirX was 0), move Y.
                    // This makes zookeeper move one cardinal step per tick based on BFS.
                    else if (dirToTargetY != 0)
                    {
                        if (nextZooY >= 0 && nextZooY < height && !walls[zooX, nextZooY])
                        {
                            zooY = nextZooY;
                        }
                    }
                }

                ticksUntilRecalc--;
                bool stuck = (
                    zooX == prevZooX && zooY == prevZooY && (dirToTargetX != 0 || dirToTargetY != 0)
                );
                if (
                    ticksUntilRecalc == 0
                    || (Math.Abs(zooX - botX) <= 1 && Math.Abs(zooY - botY) <= 1)
                    || stuck
                )
                {
                    ticksUntilRecalc = RETARGET_INTERVAL;
                    if (!botInCage)
                    {
                        ComputeGhostDirection(
                            botX,
                            botY,
                            zooX,
                            zooY,
                            out dirToTargetX,
                            out dirToTargetY
                        );
                    }
                    else
                    {
                        dirToTargetX = 0;
                        dirToTargetY = 0;
                    }
                }
            }

            if (zooX == botX && zooY == botY && !botInCage)
            {
                score -= 500;
                botX = botSpawnX;
                botY = botSpawnY;
                botInCage = true;
                dirToTargetX = 0;
                dirToTargetY = 0;
                break;
            }
        }
        return score;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanMove(int x, int y, int move)
    {
        if (walls == null)
            return false; // Check for null
        switch (move)
        {
            case 0:
                return y > 0 && !walls[x, y - 1]; // Up
            case 1:
                return y < height - 1 && !walls[x, y + 1]; // Down
            case 2:
                return x > 0 && !walls[x - 1, y]; // Left
            case 3:
                return x < width - 1 && !walls[x + 1, y]; // Right
            default:
                return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyMove(int move, ref int x, ref int y)
    {
        if (walls == null)
            return; // Check for null
        int originalX = x;
        int originalY = y;
        switch (move)
        { // 0=Up, 1=Down, 2=Left, 3=Right
            case 0:
                if (y > 0 && !walls[x, y - 1])
                    y--;
                break; // Up
            case 1:
                if (y < height - 1 && !walls[x, y + 1])
                    y++;
                break; // Down
            case 2:
                if (x > 0 && !walls[x - 1, y])
                    x--;
                break; // Left
            case 3:
                if (x < width - 1 && !walls[x + 1, y])
                    x++;
                break; // Right
        }
        // If move results in no change (hit a wall or boundary), position remains the same.
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConsumePellet(int x, int y, ulong[] pelletBits, ref int score)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        int idx = y * width + x;
        int arrIdx = idx >> 6;
        if (arrIdx < 0 || arrIdx >= pelletBits.Length)
            return;

        ulong mask = 1UL << (idx & 63);
        if ((pelletBits[arrIdx] & mask) != 0UL)
        {
            pelletBits[arrIdx] &= ~mask; // eat the pellet (remove bit)
            score += PELLET_POINTS;
        }
    }

    private static void ComputeGhostDirection(
        int targetX,
        int targetY,
        int startX,
        int startY,
        out int dirX,
        out int dirY
    )
    {
        dirX = 0;
        dirY = 0;

        if (walls == null)
        {
            Console.WriteLine("Error: walls not initialized in ComputeGhostDirection");
            return;
        }
        if (startX == targetX && startY == targetY)
            return;

        Queue<(int x, int y)> queue = new Queue<(int, int)>();
        queue.Enqueue((startX, startY));

        // Using a 2D array for cameFrom
        (int, int)[,] cameFrom = new (int, int)[width, height];
        HashSet<(int, int)> visited = new HashSet<(int, int)>();
        visited.Add((startX, startY));

        bool targetFound = false;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            int cx = current.x;
            int cy = current.y;

            if (cx == targetX && cy == targetY)
            {
                targetFound = true;
                break;
            }

            int[] dxNeighbors = { 0, 0, -1, 1 }; // Order: Up, Down, Left, Right (relative to current)
            int[] dyNeighbors = { -1, 1, 0, 0 }; // dy for Up is -1, dy for Down is +1

            for (int i = 0; i < 4; i++)
            {
                int nx = cx + dxNeighbors[i];
                int ny = cy + dyNeighbors[i];

                if (
                    nx >= 0
                    && nx < width
                    && ny >= 0
                    && ny < height
                    && !walls[nx, ny]
                    && !visited.Contains((nx, ny))
                )
                {
                    visited.Add((nx, ny));
                    cameFrom[nx, ny] = (cx, cy); // Store that (nx,ny) was reached from (cx,cy)
                    queue.Enqueue((nx, ny));
                }
            }
        }

        if (targetFound)
        {
            int pathX = targetX;
            int pathY = targetY;
            // Trace back from target to find the cell that was the direct child of startX, startY
            while (pathX != startX || pathY != startY) // Loop until we are back at the start node
            {
                // Check if cameFrom[pathX, pathY] would lead to out of bounds if start node has no predecessor set
                // This should be safe because if targetFound is true, a path exists and cameFrom is populated along it.
                (int prevX, int prevY) = cameFrom[pathX, pathY];

                if (prevX == startX && prevY == startY)
                {
                    // (pathX, pathY) is the first step taken from (startX, startY)
                    dirX = pathX - startX;
                    dirY = pathY - startY;
                    return;
                }
                pathX = prevX;
                pathY = prevY;

                // Safety break: if we somehow get into an infinite loop (e.g. malformed cameFrom or disconnected graph segment not handled by visited)
                // This is unlikely with a correct BFS on a grid but good for robustness.
                if (pathX == targetX && pathY == targetY && (dirX == 0 && dirY == 0))
                {
                    // This means we looped back to target without finding the start, indicates a problem.
                    Console.WriteLine(
                        $"Warning: BFS path reconstruction loop detected from ({startX},{startY}) to ({targetX},{targetY}). Path might be incorrect."
                    );
                    break; // Exit to fallback
                }
            }
        }

        // Fallback if BFS fails (e.g. target unreachable or error in reconstruction)
        if (dirX == 0 && dirY == 0 && !(startX == targetX && startY == targetY))
        {
            Console.WriteLine(
                $"Warning: BFS could not find path from ({startX},{startY}) to ({targetX},{targetY}). Using simple direction."
            );
            // Simplified fallback (can get stuck at walls)
            if (targetX > startX && startX + 1 < width && !walls[startX + 1, startY])
                dirX = 1;
            else if (targetX < startX && startX - 1 >= 0 && !walls[startX - 1, startY])
                dirX = -1;

            if (dirX == 0)
            { // Only try Y if X is not chosen or is blocked
                if (targetY > startY && startY + 1 < height && !walls[startX, startY + 1])
                    dirY = 1;
                else if (targetY < startY && startY - 1 >= 0 && !walls[startX, startY - 1])
                    dirY = -1;
            }
        }
    }
}
