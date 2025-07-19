using System.Diagnostics;
using StaticHeuro.Heuristics;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;
using Serilog.Core;
using System.Linq;
using System.Collections.Generic;
using Marvijo.Zooscape.Bots.Common.Utils;

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
// Back-off timer to keep the bot in essential-only mode for a few ticks
private const int ESSENTIAL_BACKOFF_DURATION = 10;
private int _essentialBackoffTicksRemaining = 0; // Counts down every tick once engaged
    
    // State synchronization recovery tracking
    private int _consecutiveDiscrepancies = 0;
    private int _lastDiscrepancyTick = -1;
    private const int MAX_CONSECUTIVE_DISCREPANCIES = 3;
    
    // Action repetition prevention
    private readonly Queue<BotAction> _recentActions = new();
    private const int ACTION_HISTORY_SIZE = 5;
    
    // Emergency fallback mode
    private bool _emergencyFallbackMode = false;
    private int _emergencyFallbackStartTick = -1;
    private const int EMERGENCY_FALLBACK_DURATION = 10;

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
    private string ShortBotId => BotId.ToString().Substring(0, 3);
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
        
        // Position data cleared for sync recovery (logging suppressed for performance)
    }

    public void SetBotId(Guid botId)
    {
        _logger.Information(
            "SetBotId called. Received BotId: {ReceivedBotId}. Current BotId before set: {CurrentBotId}",
            botId,
            ShortBotId
        );
        BotId = botId;
        _logger.Information("BotId after set: {UpdatedBotId}", ShortBotId);
    }

    public BotAction GetAction(GameState gameState)
    {
        // Start timing immediately for budget guard
        var actionStopwatch = Stopwatch.StartNew();
        
        try
        {

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
                        
                        // Position verification (logging suppressed for performance)
                        
                        // If positions don't match and we weren't at spawn point (which could indicate a respawn)
                        if (currentPos != expectedPos && !(me.X == me.SpawnX && me.Y == me.SpawnY))
                        {
                            _logger.Warning(
                                "[PositionSync] DISCREPANCY! Bot {ShortBotId} sent {Action} on tick {LastTick} expecting to move to ({ExpectedX}, {ExpectedY}) but is actually at ({ActualX}, {ActualY}) on tick {CurrentTick}",
                                ShortBotId,
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
                            ClearStalePositionData();
                        }
                    }
                }
                else if (gameState.Tick > _lastActionTick + 1)
                {
                    _logger.Warning(
                        "[PositionSync] TICK_SKIP! Expected tick {ExpectedTick} but got {ActualTick} for Bot {ShortBotId}",
                        _lastActionTick + 1,
                        gameState.Tick,
                        ShortBotId
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
                _logger.Error("GetAction received null GameState for Bot {ShortBotId}. Returning default action.", ShortBotId);
                return BotAction.Up; // Safe default if gameState is null - no animal context available
            }
            
            // Check if we're already running late before computing action
            var preCheckElapsed = actionStopwatch.ElapsedMilliseconds;
            if (preCheckElapsed >= HARD_DEADLINE_MS)
            {
                _logger.Warning(
                    "[CRITICAL_TIMEOUT] T{Tick} Bot:{ShortBotId} already exceeded {Deadline}ms deadline with {Elapsed}ms before action computation - returning safe fallback",
                    gameState.Tick,
                    ShortBotId,
                    HARD_DEADLINE_MS,
                    preCheckElapsed
                );
                
                // Clear expected position to prevent mismatch logs
                _expectedNextPosition = null;
                _lastActionSent = null;
                _lastActionTick = -1;
                
                // Activate essential-heuristics-only mode for a back-off period
                _essentialBackoffTicksRemaining = ESSENTIAL_BACKOFF_DURATION;
                _forceEssentialHeuristicsOnly = true;
                if (_lateTickCount >= 3)
                {
                    _logger.Warning(
                        "[LATE_TICK_RECOVERY] Bot:{ShortBotId} had {LateCount} consecutive late ticks - switching to essential heuristics only",
                        ShortBotId,
                        _lateTickCount
                    );
                    _forceEssentialHeuristicsOnly = true; // will be disabled after back-off timer expires
                }
                
                // Return safe fallback action without computing expensive heuristics
                var me = gameState.Animals?.FirstOrDefault(a => a.Id == BotId);
                if (me != null)
                {
                    return GetSafeFallbackAction(gameState, me);
                }
                return BotAction.Up;
            }
            
            // FAST-PATH: If a pellet is directly adjacent, immediately move onto it to guarantee pellet collection and save time
            var meInstant = gameState.Animals?.FirstOrDefault(a => a.Id == BotId);
            if (meInstant != null)
            {
                var instantAction = TryImmediatePelletMove(gameState, meInstant);
                if (instantAction.HasValue)
                {
                    // Set expected position for next tick
                    if (IsMovementAction(instantAction.Value))
                    {
                        var expectedPosFast = CalculateExpectedPosition(meInstant.X, meInstant.Y, instantAction.Value);
                        _expectedNextPosition = expectedPosFast;
                        _lastActionSent = instantAction.Value;
                        _lastActionTick = gameState.Tick;
                    }
                    _logger.Debug("[FAST_PELLET] T{Tick} Moving {Action} to collect adjacent pellet", gameState.Tick, instantAction.Value);
                    return instantAction.Value;
                }
            }

            var actionResult = GetActionWithScores(gameState);
            var chosenAction = actionResult.ChosenAction;
            
            // Check if we exceeded the hard deadline AFTER computation
            actionStopwatch.Stop();
            var elapsedMs = actionStopwatch.ElapsedMilliseconds;
            
            if (elapsedMs >= HARD_DEADLINE_MS)
            {
                _logger.Warning(
                    "[CRITICAL_TIMEOUT] T{Tick} Bot:{ShortBotId} exceeded {Deadline}ms deadline with {Elapsed}ms during action computation - suppressing computed action",
                    gameState.Tick,
                    ShortBotId,
                    HARD_DEADLINE_MS,
                    elapsedMs
                );
                
                // Clear expected position to prevent mismatch logs
                _expectedNextPosition = null;
                _lastActionSent = null;
                _lastActionTick = -1;
                
                // Activate essential-heuristics-only mode for a back-off period
                _essentialBackoffTicksRemaining = ESSENTIAL_BACKOFF_DURATION;
                _forceEssentialHeuristicsOnly = true;
                if (_lateTickCount >= 3)
                {
                    _logger.Warning(
                        "[LATE_TICK_RECOVERY] Bot:{ShortBotId} had {LateCount} consecutive late ticks - switching to essential heuristics only",
                        ShortBotId,
                        _lateTickCount
                    );
                    _forceEssentialHeuristicsOnly = true; // will be disabled after back-off timer expires
                }
                
                // Return safe fallback action instead of the computed one
                var meForFallback = gameState.Animals?.FirstOrDefault(a => a.Id == BotId);
                if (meForFallback != null)
                {
                    return GetSafeFallbackAction(gameState, meForFallback);
                }
                return BotAction.Up;
            }
            else
            {
                // Successful timing – decrement essential back-off timer if active
                if (_essentialBackoffTicksRemaining > 0)
                {
                    _essentialBackoffTicksRemaining--;
                    if (_essentialBackoffTicksRemaining == 0)
                    {
                        _forceEssentialHeuristicsOnly = false;
                        _logger.Information(
                            "[TIMING_RECOVERY] Bot:{ShortBotId} back under deadline ({Elapsed}ms < {Deadline}ms); resuming full heuristics",
                            ShortBotId,
                            elapsedMs,
                            HARD_DEADLINE_MS
                        );
                    }
                }
            }
            
            // Set position expectations for next tick
            var me2 = gameState.Animals?.FirstOrDefault(a => a.Id == BotId);
            if (me2 != null)
            {
                
                // Set expected position for next tick verification (only for movement actions)
                if (IsMovementAction(chosenAction))
                {
                    var expectedPos = CalculateExpectedPosition(me2.X, me2.Y, chosenAction);
                    if (IsLegalMove(gameState, me2, chosenAction))
                    {
                        _expectedNextPosition = expectedPos;
                        _lastActionSent = chosenAction;
                        _lastActionTick = gameState.Tick;
                    }
                    // Note: Illegal move detected (logging suppressed for performance)
                }
            }
            
            // Single consolidated log line per tick with all required information
            if (me2 != null)
            {
                _logger.Information(
                    "T{Tick} ({CurX},{CurY}) {Action} {Elapsed}ms {Score}pts",
                    gameState.Tick,
                    me2.X,
                    me2.Y,
                    chosenAction,
                    actionStopwatch.ElapsedMilliseconds,
                    me2.Score
                );
            }
            return chosenAction;
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                "Error in GetAction for Bot {ShortBotId} on tick {Tick}. Exception: {ExceptionType}: {ExceptionMessage}",
                ShortBotId,
                gameState?.Tick ?? -1,
                ex.GetType().Name,
                ex.Message
            );

            // Clear tracking on exception to prevent cascading issues
            _expectedNextPosition = null;
            _lastActionSent = null;
            _lastActionTick = -1;

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
                "GetActionWithScores received null GameState for Bot {ShortBotId}. Returning default action.",
                ShortBotId
            );
            return (BotAction.Up, new Dictionary<BotAction, decimal>(), new List<ScoreLog>());
        }

        if (gameState.Animals == null)
        {
            _logger.Error(
                "GameState.Animals is null for Bot {ShortBotId} on tick {Tick}. Returning default action.",
                ShortBotId,
                currentTick
            );
            return (BotAction.Up, new Dictionary<BotAction, decimal>(), new List<ScoreLog>());
        }

        if (gameState.Cells == null)
        {
            _logger.Error(
                "GameState.Cells is null for Bot {ShortBotId} on tick {Tick}. Returning default action.",
                ShortBotId,
                currentTick
            );
            return (BotAction.Up, new Dictionary<BotAction, decimal>(), new List<ScoreLog>());
        }

        // Check if _heuristics is properly initialized
        if (_heuristics == null)
        {
            _logger.Error(
                "HeuristicsManager is null for Bot {ShortBotId} on tick {Tick}. This should never happen! Returning default action.",
                ShortBotId,
                currentTick
            );
            return (BotAction.Up, new Dictionary<BotAction, decimal>(), new List<ScoreLog>());
        }

        if (_logger != null && this.LogHeuristicScores)
        {
            _logger.Information(
                "\n===== Evaluating Potential Moves for Bot {ShortBotId} on tick {Tick} ====",
                ShortBotId,
                currentTick
            );
        }

        // Identify this bot's animal
        var me = gameState.Animals.FirstOrDefault(a => a.Id == BotId);

        if (me == null)
        {
            _logger.Error(
                "Bot's animal with ID {ShortBotId} not found in current game state on tick {Tick}. Available animals: [{AvailableAnimals}]",
                ShortBotId,
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
            "Bot {ShortBotId} at position ({X}, {Y}) on tick {Tick}",
            ShortBotId,
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
                        "Action {Action} blocked: No cell found at ({X}, {Y}) for bot {ShortBotId} on tick {Tick}",
                        actionType,
                        nx,
                        ny,
                        ShortBotId,
                        currentTick
                    );
                }
                else if (targetCell.Content == CellContent.Wall)
                {
                    _logger.Debug(
                        "Action {Action} blocked: Wall detected at ({X}, {Y}) for bot {ShortBotId} on tick {Tick}",
                        actionType,
                        nx,
                        ny,
                        ShortBotId,
                        currentTick
                    );
                }
                else
                {
                    legalActions.Add(actionType);
                    _logger.Debug(
                        "Action {Action} is legal: Can move to ({X}, {Y}) with content {Content} for bot {ShortBotId} on tick {Tick}",
                        actionType,
                        nx,
                        ny,
                        targetCell!.Content,
                        ShortBotId,
                        currentTick
                    );
                }
            }
        }

        // Ensure we always have at least one legal action
        if (!legalActions.Any())
        {
            _logger.Warning(
                "No legal actions found for bot {ShortBotId} at ({X}, {Y}) on tick {Tick}! Adding fallback actions.",
                ShortBotId,
                me!.X,
                me.Y,
                currentTick
            );

            // Use safe fallback action instead of adding all movement actions
            var safeFallback = GetSafeFallbackAction(gameState, me);
            legalActions.Add(safeFallback);
            _logger.Warning(
                "Using safe fallback action {Action} for bot {ShortBotId} at ({X}, {Y}) on tick {Tick}",
                safeFallback, ShortBotId, me!.X, me.Y, currentTick
            );
        }

        _logger.Debug(
            "Legal actions for bot {ShortBotId} on tick {Tick}: [{Actions}]",
            ShortBotId,
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
        // Heuristic evaluation completed (logging suppressed for performance)

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



        // Verbose heuristic logging suppressed for performance (enable LogHeuristicScores if needed for debugging)

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
                "ProcessState received null GameState for Bot {ShortBotId}. Returning default action.",
                ShortBotId
            );
            return new BotCommand { Action = BotAction.Up };
        }

        if (state.Animals == null)
        {
            _logger.Error(
                "ProcessState received GameState with null Animals collection for Bot {ShortBotId} on tick {Tick}. Returning default action.",
                ShortBotId,
                state.Tick
            );
            return new BotCommand { Action = BotAction.Up };
        }

        if (state.Cells == null)
        {
            _logger.Error(
                "ProcessState received GameState with null Cells collection for Bot {ShortBotId} on tick {Tick}. Returning default action.",
                ShortBotId,
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
            // Expectation tracking is handled in GetAction; no assignments here.
            
            // Position expectation tracking (logging suppressed for performance)
        }

        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;

        // Performance monitoring (logging consolidated in GetAction for single line per tick)
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

        // FIRST, attempt to collect an adjacent pellet if one exists
        var pelletMove = TryImmediatePelletMove(gameState, me);
        if (pelletMove.HasValue)
            return pelletMove.Value;

        // Build list of legal candidate moves and evaluate distance to nearest zookeeper
        var dirMeta = new (BotAction action, int dx, int dy)[]
        {
            (BotAction.Up, 0, -1),
            (BotAction.Down, 0, 1),
            (BotAction.Left, -1, 0),
            (BotAction.Right, 1, 0)
        };

        var candidates = new List<(BotAction action, int minDist, bool hasPellet)>();

        foreach (var (action, dx, dy) in dirMeta)
        {
            int nx = me.X + dx;
            int ny = me.Y + dy;
            var cell = gameState.Cells.FirstOrDefault(c => c.X == nx && c.Y == ny);
            if (cell == null || cell.Content == CellContent.Wall)
                continue; // illegal move

            // Compute Manhattan distance from the new position to the closest zookeeper
            int minDist = int.MaxValue;
            if (gameState.Zookeepers != null && gameState.Zookeepers.Count > 0)
            {
                foreach (var zk in gameState.Zookeepers)
                {
                    int dist = BotUtils.ManhattanDistance(nx, ny, zk.X, zk.Y);
                    if (dist < minDist) minDist = dist;
                }
            }
            else
            {
                // No zookeepers – treat as extremely safe
                minDist = int.MaxValue;
            }

            bool hasPellet = cell.Content == CellContent.Pellet;
            candidates.Add((action, minDist, hasPellet));
        }

        if (candidates.Count > 0)
        {
            // Select the move(s) that maximize the distance to the nearest zookeeper
            int safestDist = candidates.Max(c => c.minDist);
            var safestMoves = candidates.Where(c => c.minDist == safestDist).ToList();

            // Prefer moves that also collect a pellet
            var pelletMoveOption = safestMoves.FirstOrDefault(c => c.hasPellet);
            if (pelletMoveOption.action != default)
                return pelletMoveOption.action;

            // Fallback to deterministic preference order to avoid oscillation
            var preferenceOrder = new[] { BotAction.Right, BotAction.Down, BotAction.Left, BotAction.Up };
            foreach (var pref in preferenceOrder)
            {
                var match = safestMoves.FirstOrDefault(c => c.action == pref);
                if (match.action != default)
                    return match.action;
            }

            // As an ultimate fallback, return the first safest move
            return safestMoves[0].action;
        }

        // If somehow no direction is legal, return Up as absolute last resort
        return BotAction.Up;
    }

    /// <summary>
    /// Checks for an immediate pellet adjacent to the bot and returns the corresponding action if found.
    /// Returns null if no adjacent pellet exists.
    /// </summary>
    private BotAction? TryImmediatePelletMove(GameState gameState, Animal me)
    {
        if (gameState?.Cells == null)
            return null;

        // Direction preference order: Right, Down, Left, Up (consistent with GetSafeFallbackAction)
        var dirs = new (BotAction action, int dx, int dy)[]
        {
            (BotAction.Right, 1, 0),
            (BotAction.Down, 0, 1),
            (BotAction.Left, -1, 0),
            (BotAction.Up, 0, -1)
        };

        foreach (var (action, dx, dy) in dirs)
        {
            int nx = me.X + dx;
            int ny = me.Y + dy;

            var cell = gameState.Cells.FirstOrDefault(c => c.X == nx && c.Y == ny);
            if (cell != null && cell.Content == CellContent.Pellet && IsLegalMove(gameState, me, action))
            {
                return action;
            }
        }

        return null;
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
