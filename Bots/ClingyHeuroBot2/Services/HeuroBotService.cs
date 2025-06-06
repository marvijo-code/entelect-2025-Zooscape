using ClingyHeuroBot2.Heuristics;
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
    private readonly HeuristicLogHelper _logHelper; // Added field
    public bool LogHeuristicScores { get; set; } = false;

    public HeuroBotService(ILogger? logger = null)
    {
        _logger = logger ?? Logger.None;
        var weights = WEIGHTS.GetWeights();
        _logHelper = new HeuristicLogHelper(); // Initialize the field
        _heuristics = new HeuristicsManager(_logger, _logHelper, weights);
    }

    public Guid BotId { get; set; }
    private BotAction? _previousAction = null;

    // Track visit counts per cell to discourage repeat visits
    private Dictionary<(int x, int y), int> _visitCounts = new();

    // Track visited quadrants for exploration incentive
    private HashSet<int> _visitedQuadrants = new();

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
        var actions = Enum.GetValues<BotAction>().Cast<BotAction>();

        // Filter legal moves: avoid walls
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
            var cell = gameState.Cells.FirstOrDefault(c => c.X == nx && c.Y == ny);
            if (cell != null && cell.Content != CellContent.Wall)
                legalActions.Add(a);
        }
        if (!legalActions.Any())
            legalActions.Add(BotAction.Up);

        BotAction bestAction = BotAction.Up;
        decimal bestScore = decimal.MinValue;
        var actionScores = new Dictionary<BotAction, decimal>();

        foreach (var action in legalActions)
        {
            var score = _heuristics.ScoreMove(gameState, me, action, LogHeuristicScores);
            if (_logger != null && LogHeuristicScores)
            {
                _logger.Information("Action {Action} - Initial score: {Score}", action, score);
            }
            // Penalty for reversing the previous move
            if (_previousAction.HasValue && IsOpposite(_previousAction.Value, action))
            {
                decimal reverseMovePenaltyValue = WEIGHTS.ReverseMovePenalty;
                // Log this as a heuristic component
                _logHelper.LogScoreComponent(
                    _logger,
                    LogHeuristicScores,
                    "ReverseMovePenalty",
                    reverseMovePenaltyValue,
                    1m,
                    reverseMovePenaltyValue,
                    score + reverseMovePenaltyValue
                );
                score += reverseMovePenaltyValue;
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
            decimal visitWeight = WEIGHTS.VisitPenalty;
            decimal visitPenaltyContribution = rawVisitFactor * visitWeight;
            // Log this as a heuristic component
            _logHelper.LogScoreComponent(
                _logger,
                LogHeuristicScores,
                "VisitPenalty",
                rawVisitFactor,
                visitWeight,
                visitPenaltyContribution,
                score + visitPenaltyContribution
            );
            score += visitPenaltyContribution;
            if (visits == 0)
            {
                decimal unexploredBonusValue = WEIGHTS.UnexploredBonus;
                // Log this as a heuristic component
                _logHelper.LogScoreComponent(
                    _logger,
                    LogHeuristicScores,
                    "UnexploredBonus",
                    unexploredBonusValue,
                    1m,
                    unexploredBonusValue,
                    score + unexploredBonusValue
                );
                score += unexploredBonusValue;
            }
            int quad = GetQuadrant(nx, ny, gameState);
            if (!_visitedQuadrants.Contains(quad))
            {
                decimal unexploredQuadrantBonusValue = WEIGHTS.UnexploredQuadrantBonus;
                // Log this as a heuristic component
                _logHelper.LogScoreComponent(
                    _logger,
                    LogHeuristicScores,
                    "UnexploredQuadrantBonus",
                    unexploredQuadrantBonusValue,
                    1m,
                    unexploredQuadrantBonusValue,
                    score + unexploredQuadrantBonusValue
                );
                score += unexploredQuadrantBonusValue;
            }
            actionScores[action] = score;
            if (_logger != null && LogHeuristicScores)
            {
                _logger.Information(
                    "Action {Action} - Final Calculated Score: {Score}",
                    action,
                    score
                );
            }
            // Console.WriteLine($"Action {action}: Score = {score}"); // Replaced by detailed Serilog logging
            if (score > bestScore)
            {
                bestScore = score;
                bestAction = action;
            }
        }

        if (_logger != null && LogHeuristicScores)
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
