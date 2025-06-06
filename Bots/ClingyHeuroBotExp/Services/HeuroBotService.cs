using System.Collections.Concurrent;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace HeuroBot.Services;

public class HeuroBotService
{
    public HeuroBotService(string botNickname)
    {
        BotNickname = botNickname;
    }

    private Guid _botId;
    private BotAction? _previousAction = null;

    // Count consecutive direction changes to prevent oscillation
    private int _directionChangeCount = 0;

    // Track recent positions to prevent oscillation (circular queue)
    private Queue<(int x, int y)> _recentPositions = new(5); // Last 5 positions

    // Track visit counts per cell to discourage repeat visits
    private Dictionary<(int x, int y), int> _visitCounts = new();

    // Track visited quadrants for exploration incentive
    private HashSet<int> _visitedQuadrants = new();

    // Store the last found path to a pellet
    private Queue<BotAction> _currentPath = new();

    // Cache for found paths to avoid recalculating
    private ConcurrentDictionary<string, List<BotAction>> _pathCache = new();

    public string? BotNickname { get; }

    public void SetBotId(Guid botId) => _botId = botId;

    public BotCommand ProcessState(GameState state)
    {
        // Start timing the processing for performance logging
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Starting turn processing");

        // Identify this bot's animal
        var me = state.Animals.First(a => a.Id == _botId);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Current position: ({me.X}, {me.Y})");

        // Count pellets and log them
        var pelletCount = state.Cells.Count(c => c.Content == CellContent.Pellet);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Pellets on board: {pelletCount}");
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Animals on board: {state.Animals.Count}");

        // Update visit count for current cell
        var currentPos = (me.X, me.Y);
        if (_visitCounts.ContainsKey(currentPos))
            _visitCounts[currentPos]++;
        else
            _visitCounts[currentPos] = 1;

        // Mark current quadrant visited
        int currentQuadrant = GetQuadrant(me.X, me.Y, state);
        if (!_visitedQuadrants.Contains(currentQuadrant))
            _visitedQuadrants.Add(currentQuadrant);

        // Get legal moves first
        var legalActions = GetLegalMoves(state, me);
        if (!legalActions.Any())
            legalActions.Add(BotAction.Up);

        // Clear path cache periodically to avoid stale data
        if (_pathCache.Count > 100 || pelletCount % 50 == 0)
        {
            _pathCache.Clear();
        }

        BotAction bestAction = BotAction.Up;
        decimal bestScore = decimal.MinValue;

        // EMERGENCY CHECK: Check if a zookeeper is immediately adjacent
        var emergencyZookeeperAction = GetEmergencyZookeeperAvoidanceMove(state, me, legalActions);
        if (emergencyZookeeperAction.HasValue)
        {
            Console.WriteLine(
                $"{BotNickname}: EMERGENCY ZOOKEEPER AVOIDANCE! Taking action: {emergencyZookeeperAction.Value}"
            );
            _previousAction = emergencyZookeeperAction.Value;

            // Clear path cache as we need to recalculate after emergency move
            _currentPath.Clear();

            // Stop timing and log performance
            sw.Stop();
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] EMERGENCY MOVE: {emergencyZookeeperAction.Value}"
            );
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] Processing completed in {sw.ElapsedMilliseconds}ms"
            );

            return new BotCommand() { Action = emergencyZookeeperAction.Value };
        }

        // STEP 1: Check if we're adjacent to a pellet - if so, go there immediately (unless unsafe)
        var adjacentPelletAction = FindAdjacentPellet(state, me);
        if (adjacentPelletAction.HasValue)
        {
            // Check if the move is safe from zookeeper
            if (IsSafeFromZookeepers(state, me, adjacentPelletAction.Value))
            {
                Console.WriteLine(
                    $"{BotNickname}: Found adjacent pellet! Taking immediate action: {adjacentPelletAction.Value}"
                );
                _previousAction = adjacentPelletAction.Value;

                // Stop timing and log performance
                sw.Stop();
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] Selected action: {adjacentPelletAction.Value}"
                );
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] Processing completed in {sw.ElapsedMilliseconds}ms"
                );

                return new BotCommand() { Action = adjacentPelletAction.Value };
            }
            else
            {
                Console.WriteLine(
                    $"{BotNickname}: Found pellet but avoiding due to nearby zookeeper!"
                );
            }
        }

        // STEP 2: If we have a cached path, follow it (if safe from zookeepers)
        if (_currentPath.Count > 0)
        {
            var nextAction = _currentPath.Peek(); // Look at next action without removing

            // Verify the action is valid and safe from zookeepers
            if (legalActions.Contains(nextAction) && IsSafeFromZookeepers(state, me, nextAction))
            {
                // Now remove the action since we're using it
                _currentPath.Dequeue();

                Console.WriteLine(
                    $"{BotNickname}: Following cached path, next action: {nextAction}"
                );
                _previousAction = nextAction;

                // Stop timing and log performance
                sw.Stop();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Selected action: {nextAction}");
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] Processing completed in {sw.ElapsedMilliseconds}ms"
                );

                return new BotCommand() { Action = nextAction };
            }
            else
            {
                // Path is invalid or unsafe, clear it
                _currentPath.Clear();
                if (!IsSafeFromZookeepers(state, me, nextAction))
                    Console.WriteLine(
                        $"{BotNickname}: Path unsafe due to zookeeper, recalculating"
                    );
                else
                    Console.WriteLine($"{BotNickname}: Path invalidated, recalculating");
            }
        }

        // STEP 3: Find path to nearest pellet
        var pathToPellet = FindPathToNearestPellet(state, me, legalActions);
        if (pathToPellet.Count > 0)
        {
            // Store the path for future use
            _currentPath = new Queue<BotAction>(pathToPellet.Skip(1)); // Skip the first action as we'll take it now
            var firstAction = pathToPellet[0];
            Console.WriteLine(
                $"{BotNickname}: Found path to pellet of length {pathToPellet.Count}, taking first action: {firstAction}"
            );
            _previousAction = firstAction;

            // Stop timing and log performance
            sw.Stop();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Selected action: {firstAction}");
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] Processing completed in {sw.ElapsedMilliseconds}ms"
            );

            return new BotCommand() { Action = firstAction };
        }

        // Fallback to regular evaluation if no direct path to pellet found
        Console.WriteLine(
            $"{BotNickname}: No direct path to pellet found, using heuristic evaluation"
        );

        // Check for immediate adjacent pellets again as a fallback
        List<BotAction> actionsToConsider = legalActions;
        var immediatePelletActions = FindImmediatePelletActions(state, me, legalActions);
        if (immediatePelletActions.Any())
        {
            Console.WriteLine(
                $"{BotNickname}: Prioritizing {immediatePelletActions.Count} actions leading to pellets"
            );
            actionsToConsider = immediatePelletActions;
        }
        // Avoid oscillation on empty cells
        else if (_directionChangeCount > 2 && _previousAction.HasValue)
        {
            var nonReversalActions = legalActions
                .Where(a => !IsOpposite(a, _previousAction.Value))
                .ToList();
            if (nonReversalActions.Any())
            {
                actionsToConsider = nonReversalActions;
                Console.WriteLine($"{BotNickname}: No pellets nearby, avoiding reversal");
            }
        }

        foreach (var action in actionsToConsider)
        {
            // Massive base score bonus for actions that lead to pellets
            decimal score = Heuristics.ScoreMove(state, me, action);

            // Calculate new position for this action
            int nx = me.X,
                ny = me.Y;
            switch (action)
            {
                case BotAction.Up:
                    ny--;
                    break;
                case BotAction.Down:
                    ny++;
                    break;
                case BotAction.Left:
                    nx--;
                    break;
                case BotAction.Right:
                    nx++;
                    break;
            }

            // Check if the target cell has a pellet - this is critical for prioritization
            var targetCell = state.Cells.FirstOrDefault(c => c.X == nx && c.Y == ny);
            bool hasPellet = targetCell != null && targetCell.Content == CellContent.Pellet;

            // Apply MASSIVE bonus for moves that lead to pellets, but check zookeeper safety
            if (hasPellet)
            {
                // Base pellet bonus
                decimal pelletBonus = 100m;

                // If there's a zookeeper collision risk, reduce the bonus significantly
                if (IsCollidingWithZookeeper(state, nx, ny))
                {
                    pelletBonus = -100m; // Never go for pellets if it means collision
                    Console.WriteLine(
                        $"{BotNickname}: DANGER! Pellet at ({nx},{ny}) has zookeeper! AVOID"
                    );
                }
                // If zookeeper adjacent but not direct collision, reduce bonus but still positive
                else if (IsAdjacentToZookeeper(state, nx, ny))
                {
                    pelletBonus = 20m; // Reduced bonus for risky pellets
                    Console.WriteLine(
                        $"{BotNickname}: Caution: Pellet at ({nx},{ny}) near zookeeper. Reduced bonus."
                    );
                }
                else
                {
                    Console.WriteLine(
                        $"{BotNickname}: Action {action} leads directly to a pellet! +{pelletBonus} bonus"
                    );
                }

                score += pelletBonus;
            }
            // If not a pellet, check if this leads toward a pellet (2 steps away)
            else if (LeadsTowardPellet(state, nx, ny))
            {
                score += 50m; // Significant bonus for moves that lead toward pellets
                Console.WriteLine(
                    $"{BotNickname}: Action {action} leads toward pellet(s)! +50 bonus"
                );
            }

            // Check if there are any zookeepers nearby that could influence our decision
            bool zookeeperNearby = state.Animals.Any(a =>
                a.Id != _botId && IsAdjacentOrClose(nx, ny, a.X, a.Y, 3)
            );

            // Apply EXTREMELY severe penalty for reversing direction unless there's a zookeeper nearby
            if (_previousAction.HasValue && IsOpposite(_previousAction.Value, action))
            {
                if (!zookeeperNearby)
                {
                    // ONLY apply heavy penalties if the target cell does NOT have a pellet
                    // If the cell has a pellet, use a much milder penalty
                    decimal reversePenalty;

                    if (hasPellet)
                    {
                        // Mild penalty for reversal if there's a pellet - prioritize collection
                        reversePenalty = WEIGHTS.ReverseMovePenalty * 1.5m;
                        Console.WriteLine(
                            $"{BotNickname}: Adding MILD reverse penalty for pellet cell: {reversePenalty} for action {action}"
                        );
                    }
                    else
                    {
                        // Still apply anti-oscillation for empty cells, but with more reasonable values
                        reversePenalty = -15m - (_directionChangeCount * 5);

                        // Stronger penalty for repeated oscillations on empty cells
                        if (_directionChangeCount > 3)
                        {
                            reversePenalty = -50m;
                        }

                        Console.WriteLine(
                            $"{BotNickname}: Adding reverse penalty {reversePenalty} for empty cell with action {action}"
                        );
                    }

                    score += reversePenalty;
                }
                else
                {
                    // Still add a penalty, but much smaller if there's a zookeeper nearby
                    decimal zookeeperAvoidancePenalty = WEIGHTS.ReverseMovePenalty * 0.5m;
                    score += zookeeperAvoidancePenalty;
                    Console.WriteLine(
                        $"{BotNickname}: Zookeeper nearby! Reduced reverse penalty for action {action}"
                    );
                }
            }
            // Visit penalty and exploration bonuses
            int visits = _visitCounts.TryGetValue((nx, ny), out var vc) ? vc : 0;
            score += WEIGHTS.VisitPenalty * visits;

            // Apply penalty for positions we've recently visited, but scale based on pellet presence
            if (_recentPositions.Contains((nx, ny)))
            {
                decimal recentPositionPenalty;

                if (hasPellet)
                {
                    // Mild penalty for revisiting cells with pellets - we want those pellets!
                    recentPositionPenalty = -3m;
                    Console.WriteLine(
                        $"{BotNickname}: Adding MILD recent position penalty {recentPositionPenalty} for PELLET cell"
                    );
                }
                else
                {
                    // Strong penalty for revisiting empty cells
                    recentPositionPenalty = -15m;
                    Console.WriteLine(
                        $"{BotNickname}: Adding recent position penalty {recentPositionPenalty} for EMPTY cell"
                    );
                }

                score += recentPositionPenalty;
            }

            if (visits == 0)
                score += WEIGHTS.UnexploredBonus;
            int quad = GetQuadrant(nx, ny, state);
            if (!_visitedQuadrants.Contains(quad))
                score += WEIGHTS.UnexploredQuadrantBonus;
            Console.WriteLine($"{BotNickname}: Action {action}: Score = {score}");
            if (score > bestScore)
            {
                bestScore = score;
                bestAction = action;
            }
        }

        // Store chosen action and mark new position to discourage oscillation and encourage exploration
        int bx = me.X,
            by = me.Y;
        switch (bestAction)
        {
            case BotAction.Up:
                by--;
                break;
            case BotAction.Down:
                by++;
                break;
            case BotAction.Left:
                bx--;
                break;
            case BotAction.Right:
                bx++;
                break;
        }
        var bestPos = (bx, by);
        if (_visitCounts.ContainsKey(bestPos))
            _visitCounts[bestPos]++;
        else
            _visitCounts[bestPos] = 1;
        int bestQuad = GetQuadrant(bx, by, state);
        if (!_visitedQuadrants.Contains(bestQuad))
            _visitedQuadrants.Add(bestQuad);

        // Update history tracking
        // Check if we're changing direction
        if (_previousAction.HasValue && IsOpposite(_previousAction.Value, bestAction))
        {
            _directionChangeCount++;
            // Log when oscillation is detected
            if (_directionChangeCount >= 2)
            {
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] WARNING: Oscillation detected! Change count: {_directionChangeCount}"
                );
            }
        }
        else
        {
            // Reset or decrease the direction change counter when not reversing
            _directionChangeCount = Math.Max(0, _directionChangeCount - 1);
        }

        // Add current position to recent positions queue
        _recentPositions.Enqueue((me.X, me.Y));
        // Keep only the most recent 5 positions
        if (_recentPositions.Count > 5)
        {
            _recentPositions.Dequeue();
        }

        // Update previous action for next turn
        _previousAction = bestAction;

        // Stop the timer and log performance metrics
        sw.Stop();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Selected action: {bestAction}");
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] Processing completed in {sw.ElapsedMilliseconds}ms"
        );

        // Alert if we're close to the time limit
        if (sw.ElapsedMilliseconds > 100)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] WARNING: Processing time {sw.ElapsedMilliseconds}ms approaching 150ms limit!"
            );
        }

        return new BotCommand() { Action = bestAction };
    }

    private static bool IsOpposite(BotAction a, BotAction b) =>
        (a == BotAction.Left && b == BotAction.Right)
        || (a == BotAction.Right && b == BotAction.Left)
        || (a == BotAction.Up && b == BotAction.Down)
        || (a == BotAction.Down && b == BotAction.Up);

    // Check if two positions are adjacent or within a certain distance
    private static bool IsAdjacentOrClose(int x1, int y1, int x2, int y2, int distance = 1)
    {
        return Math.Abs(x1 - x2) <= distance && Math.Abs(y1 - y2) <= distance;
    }

    // Check if position will be adjacent to a zookeeper
    private static bool IsAdjacentToZookeeper(GameState state, int x, int y)
    {
        return state.Zookeepers.Any(z => Math.Abs(z.X - x) <= 1 && Math.Abs(z.Y - y) <= 1);
    }

    // Check if position is in same cell as a zookeeper (collision)
    private static bool IsCollidingWithZookeeper(GameState state, int x, int y)
    {
        return state.Zookeepers.Any(z => z.X == x && z.Y == y);
    }

    // Calculates the Manhattan distance between two points
    private static int ManhattanDistance(int x1, int y1, int x2, int y2)
    {
        return Math.Abs(x1 - x2) + Math.Abs(y1 - y2);
    }

    // Check if a move is safe from zookeepers (not adjacent and not in predicted path)
    private bool IsSafeFromZookeepers(GameState state, Animal me, BotAction action)
    {
        // Calculate new position after action
        int nx = me.X,
            ny = me.Y;
        switch (action)
        {
            case BotAction.Up:
                ny--;
                break;
            case BotAction.Down:
                ny++;
                break;
            case BotAction.Left:
                nx--;
                break;
            case BotAction.Right:
                nx++;
                break;
        }

        // Check for direct collision - NEVER move into a zookeeper's cell
        if (IsCollidingWithZookeeper(state, nx, ny))
        {
            Console.WriteLine($"{BotNickname}: AVOID COLLISION - Zookeeper at ({nx},{ny})");
            return false;
        }

        // Check for adjacency - Avoid if possible, but not as strict
        if (IsAdjacentToZookeeper(state, nx, ny))
        {
            // Only avoid if we're not already adjacent (to prevent getting stuck)
            if (!IsAdjacentToZookeeper(state, me.X, me.Y))
            {
                Console.WriteLine($"{BotNickname}: AVOID ADJACENCY - Zookeeper near ({nx},{ny})");
                return false;
            }
            // If we're already adjacent, try to move away from the zookeeper if possible
            else
            {
                // Stay safe but don't get paralyzed if we're already adjacent
                Console.WriteLine(
                    $"{BotNickname}: Already adjacent to zookeeper, proceeding with caution"
                );
                return true;
            }
        }

        // Check for predicted zookeeper positions (more advanced avoidance)
        foreach (var zookeeper in state.Zookeepers)
        {
            // Simple prediction: assume zookeeper moves toward closest animal
            var targetAnimal = state
                .Animals.Where(a => a.IsViable)
                .OrderBy(a => ManhattanDistance(a.X, a.Y, zookeeper.X, zookeeper.Y))
                .FirstOrDefault();

            if (targetAnimal != null)
            {
                // Is the zookeeper targeting me?
                bool zookeeperTargetingMe = targetAnimal.Id == me.Id;

                // If I'm the target, be more cautious
                if (zookeeperTargetingMe)
                {
                    // Simple prediction of zookeeper's next position
                    int zx = zookeeper.X,
                        zy = zookeeper.Y;
                    if (zookeeper.X < me.X)
                        zx++;
                    else if (zookeeper.X > me.X)
                        zx--;
                    else if (zookeeper.Y < me.Y)
                        zy++;
                    else if (zookeeper.Y > me.Y)
                        zy--;

                    // If our next position is where we predict the zookeeper will be
                    if (nx == zx && ny == zy)
                    {
                        Console.WriteLine(
                            $"{BotNickname}: AVOID PREDICTED COLLISION - Zookeeper targeting me"
                        );
                        return false;
                    }
                }
            }
        }

        return true;
    }

    // Get emergency avoidance move if a zookeeper is very close
    private BotAction? GetEmergencyZookeeperAvoidanceMove(
        GameState state,
        Animal me,
        List<BotAction> legalActions
    )
    {
        // First check if we're in immediate danger (zookeeper 1 cell away)
        bool inDanger = state.Zookeepers.Any(z => ManhattanDistance(z.X, z.Y, me.X, me.Y) <= 1);

        if (!inDanger)
            return null;

        Console.WriteLine($"{BotNickname}: DANGER - Zookeeper very close!");

        // Find safest move - maximize distance from ALL zookeepers
        BotAction? bestAction = null;
        int maxMinDistance = -1;

        foreach (var action in legalActions)
        {
            // Calculate new position
            int nx = me.X,
                ny = me.Y;
            switch (action)
            {
                case BotAction.Up:
                    ny--;
                    break;
                case BotAction.Down:
                    ny++;
                    break;
                case BotAction.Left:
                    nx--;
                    break;
                case BotAction.Right:
                    nx++;
                    break;
            }

            // Find minimum distance to any zookeeper from this new position
            int minDistanceToZookeeper = state.Zookeepers.Min(z =>
                ManhattanDistance(nx, ny, z.X, z.Y)
            );

            // If this move increases our minimum distance, it's better
            if (minDistanceToZookeeper > maxMinDistance)
            {
                maxMinDistance = minDistanceToZookeeper;
                bestAction = action;
            }
        }

        // Only return if we found a move that gives us at least 2 cells of distance
        if (maxMinDistance >= 2)
            return bestAction;

        // If no clearly safe move, take any move that gives at least some distance
        if (bestAction.HasValue)
            return bestAction;

        return null;
    }

    // Get legal moves from current position
    private List<BotAction> GetLegalMoves(GameState state, Animal me)
    {
        var actions = Enum.GetValues<BotAction>().Cast<BotAction>();
        var legalActions = new List<BotAction>();

        foreach (var a in actions)
        {
            int nx = me.X,
                ny = me.Y;
            switch (a)
            {
                case BotAction.Up:
                    ny--;
                    break;
                case BotAction.Down:
                    ny++;
                    break;
                case BotAction.Left:
                    nx--;
                    break;
                case BotAction.Right:
                    nx++;
                    break;
            }

            var cell = state.Cells.FirstOrDefault(c => c.X == nx && c.Y == ny);
            if (cell != null && cell.Content != CellContent.Wall)
                legalActions.Add(a);
        }

        return legalActions;
    }

    // Find if a pellet is adjacent and return the action to get it
    private BotAction? FindAdjacentPellet(GameState state, Animal me)
    {
        var actions = new[] { BotAction.Up, BotAction.Down, BotAction.Left, BotAction.Right };

        foreach (var action in actions)
        {
            int nx = me.X,
                ny = me.Y;
            switch (action)
            {
                case BotAction.Up:
                    ny--;
                    break;
                case BotAction.Down:
                    ny++;
                    break;
                case BotAction.Left:
                    nx--;
                    break;
                case BotAction.Right:
                    nx++;
                    break;
            }

            var cell = state.Cells.FirstOrDefault(c => c.X == nx && c.Y == ny);
            if (cell != null && cell.Content == CellContent.Pellet)
                return action;
        }

        return null;
    }

    // Find actions that lead to pellets (immediate adjacency)
    private List<BotAction> FindImmediatePelletActions(
        GameState state,
        Animal me,
        List<BotAction> legalActions
    )
    {
        var pelletActions = new List<BotAction>();

        foreach (var action in legalActions)
        {
            int nx = me.X,
                ny = me.Y;
            switch (action)
            {
                case BotAction.Up:
                    ny--;
                    break;
                case BotAction.Down:
                    ny++;
                    break;
                case BotAction.Left:
                    nx--;
                    break;
                case BotAction.Right:
                    nx++;
                    break;
            }

            var cell = state.Cells.FirstOrDefault(c => c.X == nx && c.Y == ny);
            if (cell != null && cell.Content == CellContent.Pellet)
                pelletActions.Add(action);
        }

        return pelletActions;
    }

    // Check if a cell is one step away from a pellet
    private bool LeadsTowardPellet(GameState state, int x, int y)
    {
        // Check all adjacent cells for pellets
        return state.Cells.Any(c =>
            ((Math.Abs(c.X - x) == 1 && c.Y == y) || (Math.Abs(c.Y - y) == 1 && c.X == x))
            && c.Content == CellContent.Pellet
        );
    }

    // Find a path to the nearest pellet using breadth-first search with zookeeper avoidance
    private List<BotAction> FindPathToNearestPellet(
        GameState state,
        Animal me,
        List<BotAction> legalActions
    )
    {
        // Use cached path if available
        string cacheKey = $"{me.X},{me.Y}";
        if (_pathCache.TryGetValue(cacheKey, out var path))
        {
            // Verify first step is still safe from zookeepers
            if (path.Count > 0 && IsSafeFromZookeepers(state, me, path[0]))
            {
                Console.WriteLine($"{BotNickname}: Using cached path for position {cacheKey}");
                return path;
            }
            // If not safe, remove from cache and recalculate
            _pathCache.TryRemove(cacheKey, out _);
        }

        // Perform breadth-first search to find closest pellet with zookeeper avoidance
        var queue = new Queue<(int x, int y, List<BotAction> path)>();
        var visited = new HashSet<(int x, int y)>();

        // Initialize with all legal and safe moves from current position
        foreach (var action in legalActions)
        {
            int nx = me.X,
                ny = me.Y;
            switch (action)
            {
                case BotAction.Up:
                    ny--;
                    break;
                case BotAction.Down:
                    ny++;
                    break;
                case BotAction.Left:
                    nx--;
                    break;
                case BotAction.Right:
                    nx++;
                    break;
            }

            var cell = state.Cells.FirstOrDefault(c => c.X == nx && c.Y == ny);
            // Check both wall avoidance and zookeeper safety
            if (
                cell != null
                && cell.Content != CellContent.Wall
                &&
                // Only avoid collision, not adjacency, for initial moves to avoid paralysis
                !IsCollidingWithZookeeper(state, nx, ny)
            )
            {
                queue.Enqueue((nx, ny, new List<BotAction> { action }));
                visited.Add((nx, ny));
            }
        }

        // BFS to find nearest pellet (limiting search depth to avoid timeouts)
        int maxDepth = 12; // Increased limit to allow finding safer routes
        int searchedNodes = 0;
        int maxNodes = 1000; // Limit total nodes expanded to prevent timeouts

        while (queue.Count > 0 && maxDepth > 0 && searchedNodes < maxNodes)
        {
            var (x, y, currentPath) = queue.Dequeue();
            searchedNodes++;

            // Check if this position has a pellet
            var cell = state.Cells.FirstOrDefault(c => c.X == x && c.Y == y);
            if (cell != null && cell.Content == CellContent.Pellet)
            {
                // Found a pellet! Cache this path and return it
                _pathCache[cacheKey] = currentPath;
                return currentPath;
            }

            // If we've reached max path length, continue to next position
            if (currentPath.Count >= maxDepth)
                continue;

            // Try all four directions
            foreach (
                var action in new[]
                {
                    BotAction.Up,
                    BotAction.Down,
                    BotAction.Left,
                    BotAction.Right,
                }
            )
            {
                int nx = x,
                    ny = y;
                switch (action)
                {
                    case BotAction.Up:
                        ny--;
                        break;
                    case BotAction.Down:
                        ny++;
                        break;
                    case BotAction.Left:
                        nx--;
                        break;
                    case BotAction.Right:
                        nx++;
                        break;
                }

                // Skip if already visited or not traversable
                if (visited.Contains((nx, ny)))
                    continue;

                var nextCell = state.Cells.FirstOrDefault(c => c.X == nx && c.Y == ny);
                if (nextCell == null || nextCell.Content == CellContent.Wall)
                    continue;

                // Safety check - avoid cells where zookeepers are
                if (IsCollidingWithZookeeper(state, nx, ny))
                    continue;

                // Penalize paths adjacent to zookeepers with a higher cost (expand later)
                bool isAdjacentToZookeeper = IsAdjacentToZookeeper(state, nx, ny);

                // Add to queue with updated path
                var newPath = new List<BotAction>(currentPath) { action };

                // Add to queue - prioritize paths not adjacent to zookeepers by adding them to end of queue
                if (isAdjacentToZookeeper)
                {
                    // Lower priority by adding to end of queue
                    visited.Add((nx, ny)); // Mark as visited
                    queue.Enqueue((nx, ny, newPath)); // Add to end - will be processed later
                }
                else
                {
                    // Higher priority - use a temporary queue to add this to front
                    var tempQueue = new Queue<(int, int, List<BotAction>)>();
                    tempQueue.Enqueue((nx, ny, newPath));

                    // Add all existing queue items after this one
                    while (queue.Count > 0)
                        tempQueue.Enqueue(queue.Dequeue());

                    // Replace queue with reordered queue
                    queue = tempQueue;
                    visited.Add((nx, ny));
                }
            }
        }

        // No path found, return empty list
        return new List<BotAction>();
    }

    // Determine quadrant based on map midpoints
    private static int GetQuadrant(int x, int y, GameState state)
    {
        var xs = state.Cells.Select(c => c.X);
        var ys = state.Cells.Select(c => c.Y);
        int minX = xs.Min(),
            maxX = xs.Max();
        int minY = ys.Min(),
            maxY = ys.Max();
        int midX = (minX + maxX) / 2,
            midY = (minY + maxY) / 2;
        if (x > midX && y > midY)
            return 1;
        if (x <= midX && y > midY)
            return 2;
        if (x <= midX && y <= midY)
            return 3;
        return 4;
    }
}
