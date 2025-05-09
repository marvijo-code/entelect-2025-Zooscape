using System;
using System.Collections.Generic;
using System.Linq;
using HeuroBot.Enums;
using HeuroBot.Models;

namespace HeuroBot.Services;

public class HeuroBotService
{
    private Guid _botId;

    public void SetBotId(Guid botId) => _botId = botId;

    public BotCommand ProcessState(GameState state)
    {
        // Identify this bot's animal
        var me = state.Animals.First(a => a.Id == _botId);

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
            var score = Heuristics.ScoreMove(state, action);
            Console.WriteLine($"Action {action}: Score = {score}");
            if (score > bestScore)
            {
                bestScore = score;
                bestAction = action;
            }
        }

        return new BotCommand { Action = bestAction };
    }
}
