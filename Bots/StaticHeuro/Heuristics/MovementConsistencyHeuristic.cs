#pragma warning disable SKEXP0110
using System.Collections.Generic;
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace StaticHeuro.Heuristics;

public class MovementConsistencyHeuristic : IHeuristic
{
    public string Name => "MovementConsistency";

    /// <summary>Encourage consistent movement direction to avoid oscillation - inspired by GatherNear's consistency</summary>
    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        BotAction? previousDirection = heuristicContext.AnimalLastDirection;

        // If no committed last direction, try to infer from recent positions
        if (!previousDirection.HasValue)
        {
            var positions = heuristicContext.AnimalRecentPositions;
            if (positions != null && positions.Count >= 2)
            {
                var recentPosArray = positions.ToArray();
                var lastPos = recentPosArray[recentPosArray.Length - 1];
                var secondLastPos = recentPosArray[recentPosArray.Length - 2];

                int deltaX = lastPos.Item1 - secondLastPos.Item1;
                int deltaY = lastPos.Item2 - secondLastPos.Item2;

                if (deltaX > 0)
                    previousDirection = BotAction.Right;
                else if (deltaX < 0)
                    previousDirection = BotAction.Left;
                else if (deltaY > 0)
                    previousDirection = BotAction.Down; // Assuming Y positive is Down
                else if (deltaY < 0)
                    previousDirection = BotAction.Up; // Assuming Y negative is Up
            }
        }

        if (previousDirection.HasValue)
        {
            // Bonus for continuing in the same direction
            if (heuristicContext.CurrentMove == previousDirection.Value)
            {
                return heuristicContext.Weights.MovementConsistencyBonus;
            }

            // Penalty for reversing direction
            var opposites = new Dictionary<BotAction, BotAction>
            {
                { BotAction.Up, BotAction.Down },
                { BotAction.Down, BotAction.Up },
                { BotAction.Left, BotAction.Right },
                { BotAction.Right, BotAction.Left },
            };

            if (
                opposites.ContainsKey(previousDirection.Value)
                && opposites[previousDirection.Value] == heuristicContext.CurrentMove
            )
            {
                return -heuristicContext.Weights.MovementConsistencyPenalty;
            }
        }

        return 0m; // No consistency bonus/penalty if no previous direction could be determined
    }
}
