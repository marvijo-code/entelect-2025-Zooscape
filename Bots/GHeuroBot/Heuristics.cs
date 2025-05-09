using System;
using System.Collections.Generic;
using System.Linq;
using HeuroBot.Enums;
using HeuroBot.Models;

namespace HeuroBot.Services
{
    public static class Heuristics
    {
        public static List<Cell> ExploredCells { get; set; } = [];

        public static decimal ScoreMove(
            GameState state,
            Animal me,
            BotAction move,
            GameStrategy currentStrategy
        )
        {
            decimal score = 0m;
            var (nx, ny) = HeuristicsImpl.ApplyMoveToCoordinates(me.X, me.Y, move);

            var targetCell = state.GetCell(nx, ny);
            if (targetCell == null | targetCell.Content == CellContent.Wall)
                return decimal.MinValue;

            if (!me.IsInCage && nx == me.SpawnX && ny == me.SpawnY)
            {
                score += WEIGHTS.AvoidOwnCagePenalty;
            }

            score +=
                HeuristicsImpl.PelletSeeking(state, me, nx, ny, currentStrategy)
                * WEIGHTS.PelletSeekingUrgency;
            score += HeuristicsImpl.PelletClusterValue(state, nx, ny) * WEIGHTS.PelletClusterBonus;
            score += HeuristicsImpl.ZookeeperInteraction(state, me, nx, ny, currentStrategy);
            score +=
                HeuristicsImpl.PathSafetyAssessment(state, me, nx, ny) * WEIGHTS.PathSafetyScore;
            score += HeuristicsImpl.FutureMobilityOptions(state, nx, ny) * WEIGHTS.FutureMobility;
            score +=
                HeuristicsImpl.StrategicTerritoryValue(state, nx, ny)
                * WEIGHTS.StrategicTerritoryControl;

            if (me.IsInCage && (nx != me.SpawnX | ny != me.SpawnY))
            {
                score += WEIGHTS.CageExitUrgency;
            }

            if (
                me.PreviousAction != BotAction.DoNothing
                && HeuristicsImpl.IsOppositeMove(me.PreviousAction, move)
            )
            {
                score += WEIGHTS.ReverseMovePenalty;
            }

            if (
                targetCell != null
                && !ExploredCells.Any(c => c.X == targetCell.X && c.Y == targetCell.Y)
            )
            {
                score += WEIGHTS.UnexploredCellBonus;
            }

            score += me.TicksSinceLastCaught * WEIGHTS.TicksSinceCaughtSurvivalBonus;

            return score;
        }

        internal static class HeuristicsImpl
        {
            public static (int x, int y) ApplyMoveToCoordinates(
                int currentX,
                int currentY,
                BotAction action
            )
            {
                return action switch
                {
                    BotAction.Up => (currentX, currentY - 1),
                    BotAction.Down => (currentX, currentY + 1),
                    BotAction.Left => (currentX - 1, currentY),
                    BotAction.Right => (currentX + 1, currentY),
                    _ => (currentX, currentY),
                };
            }

            public static bool IsOppositeMove(BotAction prev, BotAction current) =>
                (prev == BotAction.Left && current == BotAction.Right)
                || (prev == BotAction.Right && current == BotAction.Left)
                || (prev == BotAction.Up && current == BotAction.Down)
                || (prev == BotAction.Down && current == BotAction.Up);

            public static int ManhattanDistance(int x1, int y1, int x2, int y2) =>
                Math.Abs(x1 - x2) + Math.Abs(y1 - y2);

            public static decimal PelletSeeking(
                GameState state,
                Animal me,
                int nextX,
                int nextY,
                GameStrategy currentStrategy
            )
            {
                if (me.IsInCage)
                    return 0m;

                var pellets = state.Cells.Where(c => c.Content == CellContent.Pellet).ToList();
                if (!pellets.Any())
                    return 0m;

                decimal pelletScore = 0;
                var targetCell = state.GetCell(nextX, nextY);
                if (targetCell?.Content == CellContent.Pellet)
                {
                    pelletScore += 50m;
                }

                var closestPellets = pellets
                    .OrderBy(p => ManhattanDistance(nextX, nextY, p.X, p.Y))
                    .Take(5);
                decimal distanceFactor = 0;
                foreach (var pellet in closestPellets)
                {
                    distanceFactor +=
                        1.0m / (ManhattanDistance(nextX, nextY, pellet.X, pellet.Y) + 1);
                }
                pelletScore += distanceFactor * 10m;

                if (currentStrategy == GameStrategy.LATE_GAME_PELLET_FOCUS)
                {
                    pelletScore *= WEIGHTS.FewPelletsRemainingFocusMultiplier;
                }
                return pelletScore;
            }

            public static decimal PelletClusterValue(GameState state, int nextX, int nextY)
            {
                int radius = 2;
                int pelletCount = 0;
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (dx == 0 && dy == 0)
                            continue;
                        var cell = state.GetCell(nextX + dx, nextY + dy);
                        if (cell?.Content == CellContent.Pellet)
                        {
                            pelletCount++;
                        }
                    }
                }
                return pelletCount * 0.5m;
            }

            public static decimal ZookeeperInteraction(
                GameState state,
                Animal me,
                int nextX,
                int nextY,
                GameStrategy currentStrategy
            )
            {
                decimal zkScore = 0;
                if (!state.Zookeepers.Any())
                    return 0m;

                foreach (var zk in state.Zookeepers)
                {
                    int distToZkAfterMove = ManhattanDistance(nextX, nextY, zk.X, zk.Y);

                    if (distToZkAfterMove <= 1)
                        zkScore += WEIGHTS.ZookeeperRepulsionDirect * 5;
                    else if (distToZkAfterMove <= 3)
                        zkScore += WEIGHTS.ZookeeperRepulsionDirect / (distToZkAfterMove);

                    zkScore += distToZkAfterMove * WEIGHTS.ZookeeperProximityPenalty;

                    Animal? currentZkTarget = state.Animals.FirstOrDefault(a =>
                        a.Id == zk.TargetAnimalId && !a.IsInCage
                    );
                    Animal? predictedZkTargetAfterMyMove = GetPredictedZookeeperTarget(
                        state,
                        zk,
                        me,
                        nextX,
                        nextY
                    );

                    if (predictedZkTargetAfterMyMove?.Id == me.Id)
                    {
                        zkScore += WEIGHTS.ZookeeperTargetedPenalty;
                        if (distToZkAfterMove <= 2)
                            zkScore *= 2;
                    }
                    else if (
                        currentZkTarget?.Id == me.Id
                        && predictedZkTargetAfterMyMove?.Id != me.Id
                    )
                    {
                        zkScore += WEIGHTS.MakeOpponentZookeeperTargetBonus * 2;
                    }
                    else if (
                        predictedZkTargetAfterMyMove != null
                        && predictedZkTargetAfterMyMove.Id != me.Id
                        && currentZkTarget?.Id != predictedZkTargetAfterMyMove.Id
                    )
                    {
                        zkScore += WEIGHTS.MakeOpponentZookeeperTargetBonus;
                    }

                    if (
                        zk.TicksUntilTargetRecalculation <= 1
                        | zk.TicksUntilTargetRecalculation % 20 <= 1
                    )
                    {
                        zkScore *= WEIGHTS.ZookeeperRecalculatingTargetSoonFactor;
                    }
                }

                if (currentStrategy == GameStrategy.CRITICAL_DANGER_EVASION)
                {
                    zkScore *= WEIGHTS.CriticalDangerEvasionMultiplier;
                }
                return zkScore;
            }

            public static Animal? GetPredictedZookeeperTarget(
                GameState state,
                Zookeeper zk,
                Animal myAnimal,
                int myNextX,
                int myNextY
            )
            {
                Animal? closestAnimal = null;
                int minDistance = int.MaxValue;

                foreach (var animal in state.Animals)
                {
                    if (animal.IsInCage)
                        continue;

                    int currentX = animal.X;
                    int currentY = animal.Y;

                    if (animal.Id == myAnimal.Id)
                    {
                        currentX = myNextX;
                        currentY = myNextY;
                    }

                    int dist = ManhattanDistance(currentX, currentY, zk.X, zk.Y);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestAnimal = animal;
                    }
                    else if (dist == minDistance)
                    {
                        if (animal.Id == zk.TargetAnimalId)
                        {
                            closestAnimal = animal;
                        }
                    }
                }
                return closestAnimal;
            }

            public static decimal PathSafetyAssessment(
                GameState state,
                Animal me,
                int nextX,
                int nextY
            )
            {
                decimal safetyScore = 0;

                var targetCell = state.GetCell(nextX, nextY);
                if (targetCell == null)
                    return -1000;

                foreach (var zk in state.Zookeepers)
                {
                    int distToZk = ManhattanDistance(nextX, nextY, zk.X, zk.Y);
                    if (distToZk == 0)
                        safetyScore -= 100;
                    else if (distToZk <= 2)
                        safetyScore -= (5.0m / distToZk);
                    else
                        safetyScore += distToZk * 0.1m;
                }
                return safetyScore;
            }

            public static decimal FutureMobilityOptions(GameState state, int nextX, int nextY)
            {
                int availableMoves = 0;
                foreach (BotAction potentialFutureAction in Enum.GetValues(typeof(BotAction)))
                {
                    if (potentialFutureAction == BotAction.DoNothing)
                        continue;
                    var (futureX, futureY) = ApplyMoveToCoordinates(
                        nextX,
                        nextY,
                        potentialFutureAction
                    );
                    var cell = state.GetCell(futureX, futureY);
                    if (cell != null && cell.Content != CellContent.Wall)
                    {
                        availableMoves++;
                    }
                }
                return availableMoves * 0.25m;
            }

            public static decimal StrategicTerritoryValue(GameState state, int nextX, int nextY)
            {
                decimal territoryScore = 0;
                int radius = 5;

                var pelletsInVicinity = state.Cells.Where(c =>
                    c.Content == CellContent.Pellet
                    && ManhattanDistance(nextX, nextY, c.X, c.Y) <= radius
                    && ManhattanDistance(nextX, nextY, c.X, c.Y) > 0
                );

                foreach (var pellet in pelletsInVicinity)
                {
                    territoryScore +=
                        1.0m / (ManhattanDistance(nextX, nextY, pellet.X, pellet.Y) + 1);
                }
                return territoryScore * 0.3m;
            }
        }
    }
}
