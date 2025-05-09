using System;
using System.Collections.Generic;
using System.Linq;
using HeuroBot.Enums;
using HeuroBot.Models;

namespace HeuroBot.Services
{
    public enum GameStrategy
    {
        EARLY_GAME_EXPLORATION,
        MID_GAME_PELLET_FOCUS,
        LATE_GAME_PELLET_FOCUS,
        CRITICAL_DANGER_EVASION,
        DEFAULT,
    }

    public class HeuroBotService
    {
        private Guid _botId;
        private HashSet<(int x, int y)> _visitedPositionsThisGame = new();

        public void SetBotId(Guid botId) => _botId = botId;

        public void ResetForNewGame()
        {
            _visitedPositionsThisGame.Clear();
        }

        private GameStrategy DetermineCurrentStrategy(GameState state, Animal me)
        {
            foreach (var zk in state.Zookeepers)
            {
                if (
                    Heuristics.HeuristicsImpl.ApplyMoveToCoordinates(
                        me.X,
                        me.Y,
                        BotAction.DoNothing
                    ) == (zk.X, zk.Y)
                    || Heuristics.HeuristicsImpl.ManhattanDistance(me.X, me.Y, zk.X, zk.Y) <= 1
                )
                {
                    var predictedZkTarget = Heuristics.HeuristicsImpl.GetPredictedZookeeperTarget(
                        state,
                        zk,
                        me,
                        me.X,
                        me.Y
                    );
                    if (predictedZkTarget?.Id == me.Id)
                        return GameStrategy.CRITICAL_DANGER_EVASION;
                }
            }

            int totalPellets = state.Cells.Count(c => c.Content == CellContent.Pellet);
            if (totalPellets < 10 || (state.MaxTicks > 0 && state.Tick > state.MaxTicks * 0.8))
            {
                return GameStrategy.LATE_GAME_PELLET_FOCUS;
            }

            if (state.MaxTicks > 0 && state.Tick > state.MaxTicks * 0.3)
            {
                return GameStrategy.MID_GAME_PELLET_FOCUS;
            }

            return GameStrategy.EARLY_GAME_EXPLORATION;
        }

        public BotCommand ProcessState(GameState state)
        {
            var me = state.Animals.FirstOrDefault(a => a.Id == _botId);
            if (me == null)
            {
                return new BotCommand { Action = BotAction.DoNothing };
            }

            me.IsInCage = (
                me.X == me.SpawnX && me.Y == me.SpawnY && state.Tick < me.TimeSpentOnSpawn + 5
            );
            if (!me.IsInCage)
                me.TimeSpentOnSpawn = state.Tick;

            var currentCellState = state.GetCell(me.X, me.Y);
            if (currentCellState != null)
                currentCellState.IsExplored = true;

            GameStrategy currentStrategy = DetermineCurrentStrategy(state, me);

            var possibleActions = Enum.GetValues(typeof(BotAction))
                .Cast<BotAction>()
                .Where(a => a != BotAction.DoNothing)
                .ToList();

            BotAction bestAction = BotAction.DoNothing;
            decimal bestScore = decimal.MinValue;
            Cell destinationCell = null;

            foreach (var action in possibleActions)
            {
                var (nx, ny) = Heuristics.HeuristicsImpl.ApplyMoveToCoordinates(me.X, me.Y, action);
                var targetCell = state.GetCell(nx, ny);

                if (targetCell == null || targetCell.Content == CellContent.Wall)
                {
                    continue;
                }

                if (!me.IsInCage && nx == me.SpawnX && ny == me.SpawnY)
                {
                    continue;
                }

                decimal score = Heuristics.ScoreMove(state, me, action, currentStrategy);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestAction = action;
                    destinationCell = targetCell;
                }
            }

            if (destinationCell != null)
            {
                Heuristics.ExploredCells.Add(destinationCell);
            }

            if (
                bestAction == BotAction.DoNothing
                && possibleActions.Any(pa =>
                    state
                        .GetCell(
                            Heuristics.HeuristicsImpl.ApplyMoveToCoordinates(me.X, me.Y, pa).x,
                            Heuristics.HeuristicsImpl.ApplyMoveToCoordinates(me.X, me.Y, pa).y
                        )
                        ?.Content != CellContent.Wall
                )
            )
            {
                var validMoves = possibleActions
                    .Where(a =>
                    {
                        var (nx, ny) = Heuristics.HeuristicsImpl.ApplyMoveToCoordinates(
                            me.X,
                            me.Y,
                            a
                        );
                        var cell = state.GetCell(nx, ny);
                        return cell != null
                            && cell.Content != CellContent.Wall
                            && !(!me.IsInCage && nx == me.SpawnX && ny == me.SpawnY);
                    })
                    .ToList();
                if (validMoves.Any())
                    bestAction = validMoves
                        .OrderByDescending(a => Heuristics.ScoreMove(state, me, a, currentStrategy))
                        .First();
            }

            me.PreviousAction = bestAction;

            return new BotCommand { Action = bestAction };
        }
    }
}
