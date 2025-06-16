using ClingyHeuroBot2.Heuristics;
using Marvijo.Zooscape.Bots.ClingyHeuroBot2.Heuristics; // For ScoreLog
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;
using Serilog.Core; // For Logger.None

namespace HeuroBot.Services;

public class HeuroBotService : IBot<HeuroBotService>
{
    private readonly ILogger _logger;
    private readonly HeuristicsManager _heuristics;
    public bool LogHeuristicScores { get; set; } = false;

    public HeuroBotService(ILogger? logger = null)
    {
        _logger = logger ?? Logger.None;
        _heuristics = new HeuristicsManager(_logger, WeightManager.Instance);
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

    public void SetBotId(Guid botId) => BotId = botId;

    public BotAction GetAction(GameState gameState)
    {
        var actionResult = GetActionWithScores(gameState);
        return actionResult.ChosenAction;
    }

    public (
        BotAction ChosenAction,
        Dictionary<BotAction, decimal> ActionScores
    ) GetActionWithScores(GameState gameState)
    {
        if (_logger != null && LogHeuristicScores)
        {
            _logger.Information("\n===== Evaluating Potential Moves for Bot {BotId} ====", BotId);
        }

        // Identify this bot's animal
        var me = gameState.Animals.First(a => a.Id == BotId);

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
                if (targetCell != null && targetCell.Content != CellContent.Wall)
                {
                    legalActions.Add(actionType);
                }
            }
        }
        if (!legalActions.Any())
            legalActions.Add(BotAction.Up);

        BotAction bestAction = BotAction.Up;
        decimal bestScore = decimal.MinValue;
        var actionScores = new Dictionary<BotAction, decimal>();
        var moveScoreLogs = new List<ScoreLog>();

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
                    currentDetailedLog.Insert(
                        currentDetailedLog.Count - 2,
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
                currentDetailedLog.Insert(
                    currentDetailedLog.Count - 2,
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
                    currentDetailedLog.Insert(
                        currentDetailedLog.Count - 2,
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
            _logger.Information(
                "===== Bot {BotId} Chose Action: {ChosenAction} with Final Score: {BestScore} =====\n",
                BotId,
                bestAction,
                Math.Round(bestScore, 4)
            );
        }
        else if (_logger != null) // Log only the chosen action if detailed scores are off
        {
            _logger.Information(
                "===== Bot {BotId} Chose Action: {ChosenAction} with Final Score: {BestScore} =====\n",
                BotId,
                bestAction,
                Math.Round(bestScore, 4)
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
        return (bestAction, actionScores);
    }

    public BotCommand ProcessState(GameState state)
    {
        var action = GetAction(state);
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
