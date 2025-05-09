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

    // Track visited positions to encourage exploration
    private HashSet<(int x, int y)> _visitedPositions = new();

    public void SetBotId(Guid botId) => _botId = botId;

    public BotCommand ProcessState(GameState state)
    {
        // Identify this bot's animal
        var me = state.Animals.First(a => a.Id == _botId);

        // Mark current position as visited
        _visitedPositions.Add((me.X, me.Y));

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
            // Bonus for moving into unexplored cells
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
            if (!_visitedPositions.Contains((nx, ny)))
                score += WEIGHTS.UnexploredBonus;
            Console.WriteLine($"Action {action}: Score = {score}");
            if (score > bestScore)
            {
                bestScore = score;
                bestAction = action;
            }
        }

        // Store chosen action and mark new position to discourage oscillation and encourage exploration
        _previousAction = bestAction;
        // Mark new position as visited
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
        _visitedPositions.Add((bx, by));
        return new BotCommand { Action = bestAction };
    }

    private static bool IsOpposite(BotAction a, BotAction b) =>
        (a == BotAction.Left && b == BotAction.Right)
        || (a == BotAction.Right && b == BotAction.Left)
        || (a == BotAction.Up && b == BotAction.Down)
        || (a == BotAction.Down && b == BotAction.Up);
}
