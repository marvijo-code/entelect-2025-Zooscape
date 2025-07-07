using ClingyHeuroBot2.Heuristics;

using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;
using Serilog.Core; // For Logger.None
using System.Diagnostics; // Added for timing

namespace ClingyHeuroBot2.Services;

public class HeuroBotService : IBot<HeuroBotService>
{
    private readonly ILogger _logger;
    private readonly HeuristicsManager _heuristics;
    public bool LogHeuristicScores { get; set; } = false;

    public HeuroBotService(ILogger? logger = null)
    {
        _logger = logger ?? Logger.None;
        
        // Get weights with null safety
        var weights = WeightManager.Instance;
        if (weights == null)
        {
            Console.WriteLine("CRITICAL: WeightManager.Instance returned null! Creating emergency weights.");
            weights = new HeuristicWeights();
        }
        
        _heuristics = new HeuristicsManager(_logger, weights);
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

    public void SetBotId(Guid botId)
    {
        _logger.Information("SetBotId called. Received BotId: {ReceivedBotId}. Current BotId before set: {CurrentBotId}", botId, BotId);
        BotId = botId;
        _logger.Information("BotId after set: {UpdatedBotId}", BotId);
    }

    public BotAction GetAction(GameState gameState)
    {
        try
        {
            var actionResult = GetActionWithScores(gameState);
            return actionResult.ChosenAction;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in GetAction for Bot {BotId} on tick {Tick}. Exception: {ExceptionType}: {ExceptionMessage}", 
                BotId, gameState?.Tick ?? -1, ex.GetType().Name, ex.Message);
            
            // Return a safe fallback action
            return BotAction.Up;
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

    public (
        BotAction ChosenAction,
        Dictionary<BotAction, decimal> ActionScores,
        List<ScoreLog> DetailedScores
    ) GetActionWithDetailedScores(GameState gameState)
    {
        var totalStopwatch = Stopwatch.StartNew();
        int currentTick = gameState?.Tick ?? -1;
        _logger.Debug("GetActionWithScores started for Bot {BotId} on tick {Tick}", BotId, currentTick);
        
        // Enhanced null safety checks
        if (gameState == null)
        {
            _logger.Error("GetActionWithScores received null GameState for Bot {BotId}. Returning default action.", BotId);
            return (BotAction.Up, new Dictionary<BotAction, decimal>(), new List<ScoreLog>());
        }

        if (gameState.Animals == null)
        {
            _logger.Error("GameState.Animals is null for Bot {BotId} on tick {Tick}. Returning default action.", BotId, currentTick);
            return (BotAction.Up, new Dictionary<BotAction, decimal>(), new List<ScoreLog>());
        }

        if (gameState.Cells == null)
        {
            _logger.Error("GameState.Cells is null for Bot {BotId} on tick {Tick}. Returning default action.", BotId, currentTick);
            return (BotAction.Up, new Dictionary<BotAction, decimal>(), new List<ScoreLog>());
        }

        // Check if _heuristics is properly initialized
        if (_heuristics == null)
        {
            _logger.Error("HeuristicsManager is null for Bot {BotId} on tick {Tick}. This should never happen! Returning default action.", BotId, currentTick);
            return (BotAction.Up, new Dictionary<BotAction, decimal>(), new List<ScoreLog>());
        }
        
        if (_logger != null && LogHeuristicScores)
        {
            _logger.Information("\n===== Evaluating Potential Moves for Bot {BotId} on tick {Tick} ====", BotId, currentTick);
        }

        // Identify this bot's animal
        var me = gameState.Animals.FirstOrDefault(a => a.Id == BotId);

        if (me == null)
        {
            _logger.Error("Bot's animal with ID {BotId} not found in current game state on tick {Tick}. Available animals: [{AvailableAnimals}]", 
                BotId, currentTick, string.Join(", ", gameState.Animals.Select(a => a.Id.ToString())));
            // Return a default action, perhaps 'None' or the safest option.
            return (BotAction.Up, new Dictionary<BotAction, decimal>(), new List<ScoreLog>()); // Defaulting to Up as BotAction.None is not available
        }

        // Log current position and wall detection
        _logger.Debug("Bot {BotId} at position ({X}, {Y}) on tick {Tick}", BotId, me.X, me.Y, currentTick);
        
        // Check for walls around current position for debugging wall-hitting behavior
        var surroundingCells = new[]
        {
            (me.X, me.Y - 1, "Up"),
            (me.X, me.Y + 1, "Down"), 
            (me.X - 1, me.Y, "Left"),
            (me.X + 1, me.Y, "Right")
        };
        
        foreach (var (x, y, direction) in surroundingCells)
        {
            var cell = gameState.Cells.FirstOrDefault(c => c.X == x && c.Y == y);
            if (cell?.Content == CellContent.Wall)
            {
                _logger.Debug("WALL DETECTED: {Direction} of bot at ({BotX}, {BotY}) is wall at ({WallX}, {WallY}) on tick {Tick}", 
                    direction, me.X, me.Y, x, y, currentTick);
            }
        }

        // Update visit count for current cell
        var currentPos = (me.X, me.Y);
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
        currentAnimalPositions.Enqueue((me.X, me.Y));
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
                int nx = me.X,
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
                    _logger.Debug("Action {Action} blocked: No cell found at ({X}, {Y}) for bot {BotId} on tick {Tick}", 
                        actionType, nx, ny, BotId, currentTick);
                }
                else if (targetCell.Content == CellContent.Wall)
                {
                    _logger.Debug("Action {Action} blocked: Wall detected at ({X}, {Y}) for bot {BotId} on tick {Tick}", 
                        actionType, nx, ny, BotId, currentTick);
                }
                else
                {
                    legalActions.Add(actionType);
                    _logger.Debug("Action {Action} is legal: Can move to ({X}, {Y}) with content {Content} for bot {BotId} on tick {Tick}", 
                        actionType, nx, ny, targetCell.Content, BotId, currentTick);
                }
            }
        }
        
        // Ensure we always have at least one legal action
        if (!legalActions.Any())
        {
            _logger.Warning("No legal actions found for bot {BotId} at ({X}, {Y}) on tick {Tick}! Adding fallback actions.", 
                BotId, me.X, me.Y, currentTick);
            
            // Add all movement actions as fallback (the game engine should handle illegal moves)
            legalActions.AddRange(new[] { BotAction.Up, BotAction.Down, BotAction.Left, BotAction.Right });
        }

        _logger.Debug("Legal actions for bot {BotId} on tick {Tick}: [{Actions}]", 
            BotId, currentTick, string.Join(", ", legalActions));

        BotAction bestAction = BotAction.Up;
        decimal bestScore = decimal.MinValue;
        var actionScores = new Dictionary<BotAction, decimal>();
        var moveScoreLogs = new List<ScoreLog>();

        var heuristicStopwatch = Stopwatch.StartNew();
        _logger.Debug("Starting heuristic evaluation loop for {ActionCount} legal actions", legalActions.Count);

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
                lastDirection // Pass animal's last committed direction
            );
            decimal currentTotalScore = currentScoreLog.TotalScore;
            List<string> currentDetailedLog = currentScoreLog.DetailedLogLines;

            // Apply penalties/bonuses specific to HeuroBotService
            // These should ideally be heuristics, but for now, we adjust the score and log them manually if needed.

            // Penalty for reversing the previous move
            if (_previousAction.HasValue && IsOpposite(_previousAction.Value, action))
            {
                decimal reverseMovePenaltyValue = WeightManager.Instance.ReverseMovePenalty;
                if (LogHeuristicScores)
                {
                    int insertIndex = Math.Max(0, currentDetailedLog.Count - 2);
                    currentDetailedLog.Insert(
                        insertIndex,
                        string.Format(
                            "    {0,-35}: Raw={1,8:F4}, Weight={2,8:F4}, Contribution={3,8:F4}, NewScore={4,8:F4}",
                            "ReverseMovePenalty",
                            reverseMovePenaltyValue,
                            1m,
                            reverseMovePenaltyValue,
                            currentTotalScore + reverseMovePenaltyValue
                        )
                    );
                }
                currentTotalScore += reverseMovePenaltyValue;
            }

            // Visit penalty and exploration bonuses
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
                decimal unexploredQuadrantBonusValue = WeightManager.Instance.UnexploredQuadrantBonus;
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
        _logger.Debug("Heuristic evaluation completed in {ElapsedTime}ms for {ActionCount} actions on tick {Tick}", 
            heuristicTimeMs, legalActions.Count, currentTick);
            
        if (heuristicTimeMs > 170)
        {
            _logger.Warning("Heuristic evaluation took {ElapsedTime}ms (>{Threshold}ms) for {ActionCount} actions on tick {Tick}, chosen action: {Action} - performance issue detected", 
                heuristicTimeMs, 170, legalActions.Count, currentTick, bestAction);
        }

        if (_logger != null && LogHeuristicScores)
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
        int bestQuad = GetQuadrant(bx, by, gameState);
        if (!_visitedQuadrants.Contains(bestQuad))
            _visitedQuadrants.Add(bestQuad);

        _animalLastDirectionsHistory[animalId] = bestAction; // Update last committed direction
        _previousAction = bestAction;

        totalStopwatch.Stop();
        var totalTimeMs = totalStopwatch.ElapsedMilliseconds;
        _logger.Debug("GetActionWithScores completed for Bot {BotId} in {TotalTime}ms on tick {Tick}, chosen action: {Action}", 
            BotId, totalTimeMs, currentTick, bestAction);
            
        if (totalTimeMs > 170)
        {
            _logger.Warning("GetActionWithScores took {TotalTime}ms (>{Threshold}ms) on tick {Tick}, chosen action: {Action} - potential stuck behavior", 
                totalTimeMs, 170, currentTick, bestAction);
        }

        return (bestAction, actionScores, moveScoreLogs);
    }

    public BotCommand ProcessState(GameState state)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Add null checks to prevent NullReferenceException
        if (state == null)
        {
            _logger.Error("ProcessState received null GameState for Bot {BotId}. Returning default action.", BotId);
            return new BotCommand { Action = BotAction.Up };
        }
        
        if (state.Animals == null)
        {
            _logger.Error("ProcessState received GameState with null Animals collection for Bot {BotId} on tick {Tick}. Returning default action.", BotId, state.Tick);
            return new BotCommand { Action = BotAction.Up };
        }
        
        if (state.Cells == null)
        {
            _logger.Error("ProcessState received GameState with null Cells collection for Bot {BotId} on tick {Tick}. Returning default action.", BotId, state.Tick);
            return new BotCommand { Action = BotAction.Up };
        }
        
        int currentTick = state.Tick;
        _logger.Debug("ProcessState started for Bot {BotId} on tick {Tick}", BotId, currentTick);
        
        var action = GetAction(state);
        
        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        _logger.Debug("ProcessState completed for Bot {BotId} in {ElapsedTime}ms on tick {Tick}, action: {Action}", 
            BotId, elapsedMs, currentTick, action);
            
        if (elapsedMs > 170)
        {
            _logger.Warning("ProcessState took {ElapsedTime}ms (>{Threshold}ms) on tick {Tick}, action: {Action} - potential performance issue", 
                elapsedMs, 170, currentTick, action);
        }
        
        return new BotCommand { Action = action };
    }

    private static bool IsOpposite(BotAction a, BotAction b) =>
        (a == BotAction.Left && b == BotAction.Right)
        || (a == BotAction.Right && b == BotAction.Left)
        || (a == BotAction.Up && b == BotAction.Down)
        || (a == BotAction.Down && b == BotAction.Up);

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
