using System;
using System.Collections.Generic;
using System.Linq;
using HeuroBot; // for WEIGHTS
using HeuroBot.Enums;
using HeuroBot.Models;

namespace HeuroBot.Services;

public class HeuroBotService
{
    private Guid _botId;
    private BotAction? _previousAction = null;

    // Track visit counts per cell to discourage repeat visits
    private Dictionary<(int x, int y), int> _visitCounts = new();

    // Track visited quadrants for exploration incentive
    private HashSet<int> _visitedQuadrants = new();

    public void SetBotId(Guid botId) => _botId = botId;

    public BotCommand ProcessState(GameState state)
    {
        // Identify this bot's animal
        var me = state.Animals.First(a => a.Id == _botId);

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
            var cell = state.Cells.FirstOrDefault(c => c.X == nx && c.Y == ny);
            if (cell != null && cell.Content != CellContent.Wall)
                legalActions.Add(a);
        }
        if (!legalActions.Any())
            legalActions.Add(BotAction.Up);

        BotAction bestAction = BotAction.Up;
        decimal bestScore = decimal.MinValue;

        foreach (var action in legalActions)
        {
            var score = Heuristics.ScoreMove(state, me, action);
            // Penalty for reversing the previous move
            if (_previousAction.HasValue && IsOpposite(_previousAction.Value, action))
                score += WEIGHTS.ReverseMovePenalty;
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
            score += WEIGHTS.VisitPenalty * visits;
            if (visits == 0)
                score += WEIGHTS.UnexploredBonus;
            int quad = GetQuadrant(nx, ny, state);
            if (!_visitedQuadrants.Contains(quad))
                score += WEIGHTS.UnexploredQuadrantBonus;
            // Console.WriteLine($"Action {action}: Score = {score}");
            if (score > bestScore)
            {
                bestScore = score;
                bestAction = action;
            }
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
        int bestQuad = GetQuadrant(bx, by, state);
        if (!_visitedQuadrants.Contains(bestQuad))
            _visitedQuadrants.Add(bestQuad);
        return new BotCommand { Action = bestAction };
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
