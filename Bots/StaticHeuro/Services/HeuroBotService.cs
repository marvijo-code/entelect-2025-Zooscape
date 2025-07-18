using System.Diagnostics;
using StaticHeuro.Heuristics;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;
using Serilog.Core;

namespace StaticHeuro.Services;

public class HeuroBotService : IBot<HeuroBotService>
{
    private readonly ILogger _logger;
    private readonly HeuristicsManager _heuristics;
    private readonly HeuristicWeights _weights;
    public bool LogHeuristicScores { get; set; } = false;
    
    // Performance and timing constants
    private const int HARD_DEADLINE_MS = 180;
    private const int SOFT_BUDGET_MS = 120;
    
    // Track last action and expected position for position tracking
    private BotAction? _lastActionSent = null;
    private (int x, int y)? _expectedNextPosition = null;
    private int _lastActionTick = -1;
    
    // Late tick tracking for robustness
    private int _lateTickCount = 0;
    private bool _forceEssentialHeuristicsOnly = false;

    public HeuroBotService(ILogger? logger = null)
    {
        _logger = logger ?? Logger.None;

        // Get weights with null safety
        _weights = WeightManager.Instance;
        if (_weights == null)
        {
            Console.WriteLine(
                "CRITICAL: WeightManager.Instance returned null! Creating emergency weights."
            );
            _weights = new HeuristicWeights();
        }

        _heuristics = new HeuristicsManager(_logger, _weights);
    }

    public Guid BotId { get; set; }
    private BotAction? _previousAction = null;

    // Track visit counts per cell to discourage repeat visits
    private Dictionary<(int x, int y), int> _visitCounts = new();

    // Track visited quadrants for exploration incentive
    private HashSet<int> _visitedQuadrants = new();

    // Keep track of recently visited positions for each animal to avoid cycles
    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        string,
        System.Collections.Generic.Queue<(int, int)>
    > _animalRecentPositionsHistory = new();

    // Track the last committed direction for each animal
    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        string,
        BotAction?
    > _animalLastDirectionsHistory = new();

    /// <summary>
    /// Clears stale position-dependent cached data when position synchronization fails.
    /// This ensures the bot uses fresh engine data for future decisions.
    /// </summary>
    private void ClearStalePositionData()
    {
        // Clear visit counts that might be based on incorrect position tracking
        _visitCounts.Clear();
        
        // Clear visited quadrants that might be stale
        _visitedQuadrants.Clear();
        
        // Clear recent position history for this bot
        var botKey = BotId.ToString();
        _animalRecentPositionsHistory.TryRemove(botKey, out _);
        _animalLastDirectionsHistory.TryRemove(botKey, out _);
        
        _logger.Information(
            "[PositionSync] RECOVERY_COMPLETE: Cleared visit counts ({VisitCountsCleared}), quadrants ({QuadrantsCleared}), and position history for bot {BotId}",
            _visitCounts.Count,
            _visitedQuadrants.Count,
            BotId
        );
    }

    public void SetBotId(Guid botId)
    {
        _logger.Information(
            "SetBotId called. Received BotId: {ReceivedBotId}. Current BotId before set: {CurrentBotId}",
            botId,
            BotId
        );
        BotId = botId;
        _logger.Information("BotId after set: {UpdatedBotId}", BotId);
    }

    public BotAction GetAction(GameState gameState)
    {
        // Start timing immediately for budget guard
        var actionStopwatch = Stopwatch.StartNew();
        
        try
        {
            // Ultra-precise telemetry for state synchronization debugging
            if (gameState != null)
            {
                var me = gameState.Animals?.FirstOrDefault(a => a.Id == BotId);
                if (me != null)
                {
                    _logger.Information(
                        "[PositionSync] BEFORE_ACTION T{Tick} Bot:{BotId} Pos:({X},{Y}) Spawn:({SpawnX},{SpawnY}) HeldPowerUp:{HeldPowerUp} ExpectedPos:{ExpectedPos} LastAction:{LastAction} LastTick:{LastTick}",
                        gameState.Tick,
                        BotId,
                        me.X,
                        me.Y,
                        me.SpawnX,
                        me.SpawnY,
                        me.HeldPowerUp?.ToString() ?? "None",
                        _expectedNextPosition?.ToString() ?? "None",
                        _lastActionSent?.ToString() ?? "None",
                        _lastActionTick
                    );
                }
            }

            // Check if we need to verify the position from the last action
            if (gameState != null && _expectedNextPosition.HasValue && _lastActionSent.HasValue && _lastActionTick != -1)
            {
                // Only check if this is the next tick after our last action
                if (gameState.Tick == _lastActionTick + 1)
                {
                    // Find our animal
                    var me = gameState.Animals?.FirstOrDefault(a => a.Id == BotId);
                    if (me != null)
                    {
                        var currentPos = (me.X, me.Y);
                        var expectedPos = _expectedNextPosition.Value;
                        
                        // Enhanced logging for position verification
                        _logger.Information(
                            "[PositionSync] VERIFY T{Tick} Bot:{BotId} Expected:({ExpectedX},{ExpectedY}) Actual:({ActualX},{ActualY}) Match:{Match} AtSpawn:{AtSpawn}",
                            gameState.Tick,
                            BotId,
                            expectedPos.Item1,
                            expectedPos.Item2,
                            currentPos.Item1,
                            currentPos.Item2,
                            currentPos == expectedPos,
                            me.X == me.SpawnX && me.Y == me.SpawnY
                        );
                        
                        // If positions don't match and we weren't at spawn point (which could indicate a respawn)
                        if (currentPos != expectedPos && !(me.X == me.SpawnX && me.Y == me.SpawnY))
                        {
                            _logger.Warning(
                                "[PositionSync] DISCREPANCY! Bot {BotId} sent {Action} on tick {LastTick} expecting to move to ({ExpectedX}, {ExpectedY}) but is actually at ({ActualX}, {ActualY}) on tick {CurrentTick}",
                                BotId,
                                _lastActionSent,
                                _lastActionTick,
                                expectedPos.Item1,
                                expectedPos.Item2,
                                currentPos.Item1,
                                currentPos.Item2,
                                gameState.Tick
                            );
                            
                            // CRITICAL FIX: Force state recovery by clearing stale tracking data
                            // This ensures the bot uses the engine's authoritative position for future decisions
                            _logger.Information(
                                "[PositionSync] RECOVERY: Clearing stale position tracking data to resync with engine state"
                            );
                            
                            // Clear any cached position-dependent data that might be stale
                            ClearStalePositionData();
                        }
                    }
                }
                else if (gameState.Tick > _lastActionTick + 1)
                {
                    _logger.Warning(
                        "[PositionSync] TICK_SKIP! Expected tick {ExpectedTick} but got {ActualTick} for Bot {BotId}",
                        _lastActionTick + 1,
                        gameState.Tick,
                        BotId
                    );
                }
                
                // Reset tracking after checking (only if no discrepancy was found)
                // If there was a discrepancy, tracking was already cleared by ClearStalePositionData()
                if (_expectedNextPosition != null)
                {
                    _expectedNextPosition = null;
                    _lastActionSent = null;
                    _lastActionTick = -1;
                }
            }
            
            if (gameState == null)
            {
                _logger.Error("GetAction received null GameState for Bot {BotId}. Returning default action.", BotId);
                return BotAction.Up; // Safe default if gameState is null - no animal context available
            }
            
            var actionResult = GetActionWithScores(gameState);
            var chosenAction = actionResult.ChosenAction;
            
            // Enhanced post-action telemetry
            var me2 = gameState.Animals?.FirstOrDefault(a => a.Id == BotId);
            if (me2 != null)
            {
                _logger.Information(
                    "[PositionSync] AFTER_ACTION T{Tick} Bot:{BotId} ChosenAction:{Action} CurrentPos:({X},{Y})",
                    gameState.Tick,
                    BotId,
                    chosenAction,
                    me2.X,
                    me2.Y
                );
                
                // Set expected position for next tick verification (only for movement actions)
                if (IsMovementAction(chosenAction))
                {
                    var expectedPos = CalculateExpectedPosition(me2.X, me2.Y, chosenAction);
                    if (IsLegalMove(gameState, me2, chosenAction))
                    {
                        _expectedNextPosition = expectedPos;
                        _lastActionSent = chosenAction;
                        _lastActionTick = gameState.Tick;
                        
                        _logger.Information(
                            "[PositionSync] EXPECTATION_SET T{Tick} Bot:{BotId} Action:{Action} ExpectedNextPos:({ExpectedX},{ExpectedY})",
                            gameState.Tick,
                            BotId,
                            chosenAction,
                            expectedPos.Item1,
                            expectedPos.Item2
                        );
                    }
                    else
                    {
                        _logger.Warning(
                            "[PositionSync] ILLEGAL_MOVE! T{Tick} Bot:{BotId} attempted illegal action {Action} from ({X},{Y})",
                            gameState.Tick,
                            BotId,
                            chosenAction,
                            me2.X,
                            me2.Y
                        );
                    }
                }
            }
            
            // Check if we exceeded the hard deadline and implement late-action handling
            actionStopwatch.Stop();
            var elapsedMs = actionStopwatch.ElapsedMilliseconds;
            
            if (elapsedMs >= HARD_DEADLINE_MS)
            {
                _logger.Warning(
                    "[CRITICAL_TIMEOUT] T{Tick} Bot:{BotId} exceeded {Deadline}ms deadline with {Elapsed}ms - suppressing action",
                    gameState.Tick,
                    BotId,
                    HARD_DEADLINE_MS,
                    elapsedMs
                );
                
                // Clear expected position to prevent mismatch logs
                _expectedNextPosition = null;
                _lastActionSent = null;
                _lastActionTick = -1;
                
                // Track late ticks for robustness
                _lateTickCount++;
                if (_lateTickCount >= 3)
                {
                    _logger.Warning(
                        "[LATE_TICK_RECOVERY] Bot:{BotId} had {LateCount} consecutive late ticks - switching to essential heuristics only",
                        BotId,
                        _lateTickCount
                    );
                    _forceEssentialHeuristicsOnly = true;
                }
                
                // Return safe fallback action instead of the computed one
                if (me2 != null)
                {
                    return GetSafeFallbackAction(gameState, me2);
                }
                return BotAction.Up;
            }
            else
            {
                // Reset late tick count on successful timing
                if (_lateTickCount > 0)
                {
                    _lateTickCount = 0;
                    _forceEssentialHeuristicsOnly = false;
                    _logger.Information(
                        "[TIMING_RECOVERY] Bot:{BotId} back under deadline ({Elapsed}ms < {Deadline}ms) - resuming normal heuristics",
                        BotId,
                        elapsedMs,
                        HARD_DEADLINE_MS
                    );
                }
            }
            
            return chosenAction;
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                "Error in GetAction for Bot {BotId} on tick {Tick}. Exception: {ExceptionType}: {ExceptionMessage}",
                BotId,
                gameState?.Tick ?? -1,
                ex.GetType().Name,
                ex.Message
            );

            // Return a safe fallback action
            if (gameState?.Animals != null)
            {
                var me = gameState.Animals.FirstOrDefault(a => a.Id == BotId);
                if (me != null)
                {
                    return GetSafeFallbackAction(gameState, me);
                }
            }
            return BotAction.Up; // Ultimate fallback if we can't find the bot - no animal context available
        }
    }

    public (
        BotAction ChosenAction,
        Dictionary<BotAction, decimal> ActionScores
    ) GetActionWithScores(GameState gameState)
    {
        var (chosenAction, actionScores, _) = GetActionWithDetailedScores(gameState);
        return (chosenAction, actionScores);
    }

    // Overload that matches TestController expectations
    public (
        BotAction ChosenAction,
        Dictionary<BotAction, decimal> ActionScores,
        List<ScoreLog> DetailedScores
    ) GetActionWithDetailedScores(GameState gameState, string animalId)
    {
        // Set the BotId to the provided animalId if it's different
        var originalBotId = BotId;
        if (!string.IsNullOrEmpty(animalId))
        {
            if (Guid.TryParse(animalId, out var parsedId))
            {
                BotId = parsedId;
            }
            else
            {
                _logger?.Warning("Failed to parse animalId '{AnimalId}' as Guid, using original BotId", animalId);
            }
        }
        
        try
        {
            return GetActionWithDetailedScores(gameState);
        }
        finally
        {
            // Restore original BotId
            BotId = originalBotId;
        }
    }

    public (
        BotAction ChosenAction,
        Dictionary<BotAction, decimal> ActionScores,
        List<ScoreLog> DetailedScores
    ) GetActionWithDetailedScores(GameState gameState)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var actionStopwatch = totalStopwatch; // Use same stopwatch for budget guard
        int currentTick = gameState?.Tick ?? -1;

        // Enhanced null safety checks
        if (gameState == null)
        {
            _logger.Error(
                "GetActionWithScores received null GameState for Bot {BotId}. Returning default action.",
                BotId
            );
            return (BotAction.Up, new Dictionary<BotAction, decimal>(), new List<ScoreLog>());
        }

        if (gameState.Animals == null)
        {
            _logger.Error(
                "GameState.Animals is null for Bot {BotId} on tick {Tick}. Returning default action.",
                BotId,
                currentTick
            );
            return (BotAction.Up, new Dictionary<BotAction, decimal>(), new List<ScoreLog>());
        }

        if (gameState.Cells == null)
        {
            _logger.Error(
                "GameState.Cells is null for Bot {BotId} on tick {Tick}. Returning default action.",
                BotId,
                currentTick
            );
            return (BotAction.Up, new Dictionary<BotAction, decimal>(), new List<ScoreLog>());
        }

        // Check if _heuristics is properly initialized
        if (_heuristics == null)
        {
            _logger.Error(
                "HeuristicsManager is null for Bot {BotId} on tick {Tick}. This should never happen! Returning default action.",
                BotId,
                currentTick
            );
            return (BotAction.Up, new Dictionary<BotAction, decimal>(), new List<ScoreLog>());
        }

        if (_logger != null && this.LogHeuristicScores)
        {
            _logger.Information(
                "\n===== Evaluating Potential Moves for Bot {BotId} on tick {Tick} ====",
                BotId,
                currentTick
            );
        }

        // Identify this bot's animal
        var me = gameState.Animals.FirstOrDefault(a => a.Id == BotId);

        if (me == null)
        {
            _logger.Error(
                "Bot's animal with ID {BotId} not found in current game state on tick {Tick}. Available animals: [{AvailableAnimals}]",
                BotId,
                currentTick,
                string.Join(", ", gameState.Animals.Select(a => a.Id.ToString()))
            );
            // Try to find any animal as fallback to determine a safe action
            var fallbackAnimal = gameState.Animals.FirstOrDefault();
            var safeFallback = fallbackAnimal != null ? GetSafeFallbackAction(gameState, fallbackAnimal) : BotAction.Up;
            _logger.Warning("Using safe fallback action: {Action}", safeFallback);
            return (safeFallback, new Dictionary<BotAction, decimal>(), new List<ScoreLog>());
        }

        // Log current position and wall detection
        _logger?.Debug(
            "Bot {BotId} at position ({X}, {Y}) on tick {Tick}",
            BotId,
            me!.X,
            me.Y,
            currentTick
        );

        // Check for walls around current position for debugging wall-hitting behavior
        var surroundingCells = new[]
        {
            (me!.X, me.Y - 1, "Up"),
            (me.X, me.Y + 1, "Down"),
            (me.X - 1, me.Y, "Left"),
            (me.X + 1, me.Y, "Right"),
        };

        foreach (var (x, y, direction) in surroundingCells)
        {
            var cell = gameState.Cells.FirstOrDefault(c => c.X == x && c.Y == y);
            if (cell?.Content == CellContent.Wall)
            {
                _logger.Debug(
                    "WALL DETECTED: {Direction} of bot at ({BotX}, {BotY}) is wall at ({WallX}, {WallY}) on tick {Tick}",
                    direction,
                    me!.X,
                    me.Y,
                    x,
                    y,
                    currentTick
                );
            }
        }

        // Update visit count for current cell
        var currentPos = (me!.X, me.Y);
        if (_visitCounts.ContainsKey(currentPos))
            _visitCounts[currentPos]++;
        else
            _visitCounts[currentPos] = 1;
        // Mark current quadrant visited
        int currentQuadrant = GetQuadrant(me.X, me.Y, gameState);
        if (!_visitedQuadrants.Contains(currentQuadrant))
            _visitedQuadrants.Add(currentQuadrant);

        // Enumerate all possible actions
        var allPossibleActions = Enum.GetValues<BotAction>().Cast<BotAction>();

        // Filter for legal moves. Start with an empty list.
        var legalActions = new List<BotAction>();

        // Check if the bot can use a held power-up. Usable power-ups are those that are not null.
        bool canUseItem = me.HeldPowerUp != null;

        // Animal-specific history for context
        string animalId = me.Id.ToString();
        var currentAnimalPositions = _animalRecentPositionsHistory.GetOrAdd(
            animalId,
            _ => new System.Collections.Generic.Queue<(int, int)>()
        );

        // Update position history (logic from old UpdatePathMemory)
        currentAnimalPositions.Enqueue((me!.X, me.Y));
        while (currentAnimalPositions.Count > 10) // Keep last 10 positions
        {
            currentAnimalPositions.Dequeue();
        }
        if (me.X == me.SpawnX && me.Y == me.SpawnY) // Clear history if respawned
        {
            currentAnimalPositions.Clear();
            _animalLastDirectionsHistory.TryRemove(animalId, out _);
        }

        _animalLastDirectionsHistory.TryGetValue(animalId, out BotAction? lastDirection);

        foreach (var actionType in allPossibleActions)
        {
            if (actionType == BotAction.UseItem)
            {
                if (canUseItem)
                {
                    legalActions.Add(actionType);
                }
            }
            else // For movement actions
            {
                int nx = me!.X,
                    ny = me.Y;
                switch (actionType)
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
                var targetCell = gameState.Cells.FirstOrDefault(c => c.X == nx && c.Y == ny);

                // Enhanced wall detection with logging
                if (targetCell == null)
                {
                    _logger.Debug(
                        "Action {Action} blocked: No cell found at ({X}, {Y}) for bot {BotId} on tick {Tick}",
                        actionType,
                        nx,
                        ny,
                        BotId,
                        currentTick
                    );
                }
                else if (targetCell.Content == CellContent.Wall)
                {
                    _logger.Debug(
                        "Action {Action} blocked: Wall detected at ({X}, {Y}) for bot {BotId} on tick {Tick}",
                        actionType,
                        nx,
                        ny,
                        BotId,
                        currentTick
                    );
                }
                else
                {
                    legalActions.Add(actionType);
                    _logger.Debug(
                        "Action {Action} is legal: Can move to ({X}, {Y}) with content {Content} for bot {BotId} on tick {Tick}",
                        actionType,
                        nx,
                        ny,
                        targetCell!.Content,
                        BotId,
                        currentTick
                    );
                }
            }
        }

        // Ensure we always have at least one legal action
        if (!legalActions.Any())
        {
            _logger.Warning(
                "No legal actions found for bot {BotId} at ({X}, {Y}) on tick {Tick}! Adding fallback actions.",
                BotId,
                me!.X,
                me.Y,
                currentTick
            );

            // Use safe fallback action instead of adding all movement actions
            var safeFallback = GetSafeFallbackAction(gameState, me);
            legalActions.Add(safeFallback);
            _logger.Warning(
                "Using safe fallback action {Action} for bot {BotId} at ({X}, {Y}) on tick {Tick}",
                safeFallback, BotId, me!.X, me.Y, currentTick
            );
        }

        _logger.Debug(
            "Legal actions for bot {BotId} on tick {Tick}: [{Actions}]",
            BotId,
            currentTick,
            string.Join(", ", legalActions)
        );



        BotAction bestAction = BotAction.Up;
        decimal bestScore = decimal.MinValue;
        var actionScores = new Dictionary<BotAction, decimal>();
        var moveScoreLogs = new List<ScoreLog>();

        var heuristicStopwatch = Stopwatch.StartNew();
        _logger.Debug(
            "Starting heuristic evaluation loop for {ActionCount} legal actions",
            legalActions.Count
        );

        foreach (var action in legalActions)
        {
            // Get initial score log from HeuristicsManager
            ScoreLog currentScoreLog = _heuristics.ScoreMove(
                gameState,
                me,
                action,
                LogHeuristicScores,
                _visitCounts,
                currentAnimalPositions, // Pass animal's recent positions
                lastDirection, // Pass animal's last committed direction
                actionStopwatch, // Pass stopwatch for budget guard
                SOFT_BUDGET_MS, // Pass soft budget limit
                _forceEssentialHeuristicsOnly // Pass essential-only flag
            );
            decimal currentTotalScore = currentScoreLog.TotalScore;
            List<string> currentDetailedLog = currentScoreLog.DetailedLogLines;

            // Apply penalties/bonuses specific to HeuroBotService
            // These should ideally be heuristics, but for now, we adjust the score and log them manually if needed.


            // Visit penalty and exploration bonuses
            int nx = me!.X,
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
            int visits = _visitCounts.TryGetValue((nx, ny), out var vc) ? vc : 0;
            decimal rawVisitFactor = visits;
            decimal visitWeight = WeightManager.Instance.VisitPenalty;
            decimal visitPenaltyContribution = rawVisitFactor * visitWeight;
            if (LogHeuristicScores)
            {
                int insertIndex = Math.Max(0, currentDetailedLog.Count - 2);
                currentDetailedLog.Insert(
                    insertIndex,
                    string.Format(
                        "    {0,-35}: Raw={1,8:F4}, Weight={2,8:F4}, Contribution={3,8:F4}, NewScore={4,8:F4}",
                        "VisitPenalty",
                        rawVisitFactor,
                        visitWeight,
                        visitPenaltyContribution,
                        currentTotalScore + visitPenaltyContribution
                    )
                );
            }
            currentTotalScore += visitPenaltyContribution;

            int quad = GetQuadrant(nx, ny, gameState);
            if (!_visitedQuadrants.Contains(quad))
            {
                decimal unexploredQuadrantBonusValue = WeightManager
                    .Instance
                    .UnexploredQuadrantBonus;
                if (LogHeuristicScores)
                {
                    int insertIndex = Math.Max(0, currentDetailedLog.Count - 2);
                    currentDetailedLog.Insert(
                        insertIndex,
                        string.Format(
                            "    {0,-35}: Raw={1,8:F4}, Weight={2,8:F4}, Contribution={3,8:F4}, NewScore={4,8:F4}",
                            "UnexploredQuadrantBonus",
                            unexploredQuadrantBonusValue,
                            1m,
                            unexploredQuadrantBonusValue,
                            currentTotalScore + unexploredQuadrantBonusValue
                        )
                    );
                }
                currentTotalScore += unexploredQuadrantBonusValue;
            }

            // Update the ScoreLog with the final adjusted score and potentially modified detailed logs
            var finalScoreLog = new ScoreLog(action, currentTotalScore, currentDetailedLog);
            moveScoreLogs.Add(finalScoreLog);
            actionScores[action] = finalScoreLog.TotalScore; // Keep this for compatibility if other parts use it

            if (finalScoreLog.TotalScore > bestScore)
            {
                bestScore = finalScoreLog.TotalScore;
                bestAction = action;
            }
        }

        heuristicStopwatch.Stop();
        var heuristicTimeMs = heuristicStopwatch.ElapsedMilliseconds;
        _logger.Debug(
            "Heuristic evaluation completed in {ElapsedTime}ms for {ActionCount} actions on tick {Tick}",
            heuristicTimeMs,
            legalActions.Count,
            currentTick
        );

        if (heuristicTimeMs > 180)
        {
            _logger.Warning(
                "Heuristic evaluation took {ElapsedTime}ms (>{Threshold}ms) for {ActionCount} actions on tick {Tick}, chosen action: {Action} - performance issue detected",
                heuristicTimeMs,
                180,
                legalActions.Count,
                currentTick,
                bestAction
            );
        }



        if (_logger != null && this.LogHeuristicScores)
        {
            _logger.Information(
                "\n=============== Move Scores Summary (Bot: {BotId}) ==============",
                BotId
            );
            foreach (var log in moveScoreLogs.OrderByDescending(l => l.TotalScore))
            {
                _logger.Information(
                    "  Move \"{Move}\": {Score}",
                    log.Move,
                    Math.Round(log.TotalScore, 4)
                );
            }
            _logger.Information("==================== End Summary ====================\n");

            _logger.Information(
                "\n============== Detailed Heuristic Scores (Bot: {BotId}) ==============",
                BotId
            );
            foreach (var log in moveScoreLogs) // Or order as preferred, e.g., by action enum order
            {
                foreach (var line in log.DetailedLogLines)
                {
                    _logger.Information(line);
                }
            }
            _logger.Information("================== End Detailed Scores ==================\n");

            // Original final choice log
            _logger.Debug(
                "===== Bot {BotId} Chose Action: {ChosenAction} with Final Score: {BestScore} on tick {Tick} =====\n",
                BotId,
                bestAction,
                Math.Round(bestScore, 4),
                currentTick
            );
        }
        else if (_logger != null) // Log only the chosen action if detailed scores are off
        {
            _logger.Debug(
                "===== Bot {BotId} Chose Action: {ChosenAction} with Final Score: {BestScore} on tick {Tick} =====\n",
                BotId,
                bestAction,
                Math.Round(bestScore, 4),
                currentTick
            );
        }

        // Store chosen action and mark new position to discourage oscillation and encourage exploration
        _previousAction = bestAction;
        int bx = me!.X,
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
        int bestQuad = GetQuadrant(bx, by, gameState);
        if (!_visitedQuadrants.Contains(bestQuad))
            _visitedQuadrants.Add(bestQuad);

        _animalLastDirectionsHistory[animalId] = bestAction; // Update last committed direction
        _previousAction = bestAction;

        totalStopwatch.Stop();
        var totalTimeMs = totalStopwatch.ElapsedMilliseconds;

        // Concise heuristic processing log
        _logger?.Debug(
            "Heuristics T{Tick} {Action} {Duration}ms",
            currentTick,
            bestAction,
            totalTimeMs
        );

        if (totalTimeMs > 180)
        {
            _logger.Warning(
                "SLOW Heuristics T{Tick} {Action} {Duration}ms - stuck behavior detected",
                currentTick,
                bestAction,
                totalTimeMs
            );
        }

        return (bestAction, actionScores, moveScoreLogs);
    }

    public BotCommand ProcessState(GameState state)
    {
        var stopwatch = Stopwatch.StartNew();

        // Add null checks to prevent NullReferenceException
        if (state == null)
        {
            _logger.Error(
                "ProcessState received null GameState for Bot {BotId}. Returning default action.",
                BotId
            );
            return new BotCommand { Action = BotAction.Up };
        }

        if (state.Animals == null)
        {
            _logger.Error(
                "ProcessState received GameState with null Animals collection for Bot {BotId} on tick {Tick}. Returning default action.",
                BotId,
                state.Tick
            );
            return new BotCommand { Action = BotAction.Up };
        }

        if (state.Cells == null)
        {
            _logger.Error(
                "ProcessState received GameState with null Cells collection for Bot {BotId} on tick {Tick}. Returning default action.",
                BotId,
                state.Tick
            );
            return new BotCommand { Action = BotAction.Up };
        }

        int currentTick = state.Tick;

        var action = GetAction(state);
        
        // Record the expected position based on the action
        var me = state.Animals?.FirstOrDefault(a => a.Id == BotId);
        if (me != null && action != BotAction.UseItem)
        {
            // Calculate expected next position based on the action
            int expectedX = me.X;
            int expectedY = me.Y;
            
            switch (action)
            {
                case BotAction.Up:
                    expectedY--;
                    break;
                case BotAction.Down:
                    expectedY++;
                    break;
                case BotAction.Left:
                    expectedX--;
                    break;
                case BotAction.Right:
                    expectedX++;
                    break;
            }
            
            // Store the action and expected position for verification in the next tick
            _lastActionSent = action;
            _expectedNextPosition = (expectedX, expectedY);
            _lastActionTick = currentTick;
            
            _logger.Debug(
                "Bot {BotId} sending action {Action} on tick {Tick}, expecting to move from ({CurrentX}, {CurrentY}) to ({ExpectedX}, {ExpectedY})",
                BotId,
                action,
                currentTick,
                me.X,
                me.Y,
                expectedX,
                expectedY
            );
        }

        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;

        // Only log if processing took notable time (>10ms) or if there's a performance issue
        if (elapsedMs > 10)
        {
            _logger.Debug(
                "ProcessState T{Tick} {Action} {Duration}ms",
                currentTick,
                action,
                elapsedMs
            );
        }

        if (elapsedMs > 180)
        {
            _logger.Warning(
                "SLOW ProcessState T{Tick} {Action} {Duration}ms - performance issue",
                currentTick,
                action,
                elapsedMs
            );
        }

        return new BotCommand { Action = action };
    }

    private static bool IsOpposite(BotAction a, BotAction b) =>
        (a == BotAction.Left && b == BotAction.Right)
        || (a == BotAction.Right && b == BotAction.Left)
        || (a == BotAction.Up && b == BotAction.Down)
        || (a == BotAction.Down && b == BotAction.Up);

    /// <summary>
    /// Gets a safe fallback action that is guaranteed to be legal based on the current game state.
    /// This prevents the bot from returning illegal moves as fallbacks.
    /// </summary>
    private BotAction GetSafeFallbackAction(GameState gameState, Animal me)
    {
        if (gameState?.Cells == null || me == null)
            return BotAction.Up; // Last resort if we can't analyze the game state

        // Try each direction in order of preference and return the first legal one
        var actionsToTry = new[] { BotAction.Right, BotAction.Down, BotAction.Left, BotAction.Up };
        
        foreach (var action in actionsToTry)
        {
            int nx = me.X, ny = me.Y;
            switch (action)
            {
                case BotAction.Up: ny--; break;
                case BotAction.Down: ny++; break;
                case BotAction.Left: nx--; break;
                case BotAction.Right: nx++; break;
            }
            
            var targetCell = gameState.Cells.FirstOrDefault(c => c.X == nx && c.Y == ny);
            if (targetCell != null && targetCell.Content != CellContent.Wall)
            {
                return action;
            }
        }
        
        // If somehow no direction is legal, return Up as absolute last resort
        return BotAction.Up;
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

    /// <summary>
    /// Determines if the given action is a movement action (not UseItem)
    /// </summary>
    private static bool IsMovementAction(BotAction action)
    {
        return action == BotAction.Up || action == BotAction.Down || 
               action == BotAction.Left || action == BotAction.Right;
    }

    /// <summary>
    /// Calculates the expected position after performing the given movement action
    /// </summary>
    private static (int x, int y) CalculateExpectedPosition(int currentX, int currentY, BotAction action)
    {
        return action switch
        {
            BotAction.Up => (currentX, currentY - 1),
            BotAction.Down => (currentX, currentY + 1),
            BotAction.Left => (currentX - 1, currentY),
            BotAction.Right => (currentX + 1, currentY),
            _ => (currentX, currentY) // For UseItem or invalid actions
        };
    }

    /// <summary>
    /// Checks if the given movement action is legal from the current position
    /// </summary>
    private bool IsLegalMove(GameState gameState, Animal animal, BotAction action)
    {
        if (!IsMovementAction(action))
            return action == BotAction.UseItem && animal.HeldPowerUp != null;

        var (targetX, targetY) = CalculateExpectedPosition(animal.X, animal.Y, action);
        var targetCell = gameState.Cells?.FirstOrDefault(c => c.X == targetX && c.Y == targetY);
        
        return targetCell != null && targetCell.Content != CellContent.Wall;
    }

}
