using System.Collections.Generic;
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class MovementConsistencyHeuristic : IHeuristic
{
    public string Name => "MovementConsistency";

    /// <summary>Encourage consistent movement direction to avoid oscillation - inspired by GatherNear's consistency</summary>
    public decimal CalculateRawScore(IHeuristicContext heuristicContext)
    {
        // Without action history, we'll use a simple heuristic based on position
        // Favor moves that continue in a straight line when possible

        // Use the recent positions to infer direction (if available through static tracking)
        string animalKey = heuristicContext.CurrentAnimal.Id.ToString();
        // Access the static _recentPositions from HeuristicsManager
        if (HeuristicsManager._recentPositions.ContainsKey(animalKey))
        {
            var positions = HeuristicsManager._recentPositions[animalKey];
            if (positions.Count >= 2)
            {
                var recentPos = positions.ToArray(); // Requires System.Linq
                var lastPos = recentPos[recentPos.Length - 1];
                var secondLastPos = recentPos[recentPos.Length - 2];

                // Infer the recent direction
                int deltaX = lastPos.Item1 - secondLastPos.Item1;
                int deltaY = lastPos.Item2 - secondLastPos.Item2;

                BotAction inferredDirection = BotAction.Up; // Default, will be overwritten
                if (deltaX > 0)
                    inferredDirection = BotAction.Right;
                else if (deltaX < 0)
                    inferredDirection = BotAction.Left;
                else if (deltaY > 0) // Assuming Y positive is Down in game coordinates
                    inferredDirection = BotAction.Down;
                else if (deltaY < 0) // Assuming Y negative is Up in game coordinates
                    inferredDirection = BotAction.Up;

                // Bonus for continuing in the same direction
                if (heuristicContext.CurrentMove == inferredDirection)
                {
                    return 0.6m;
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
                    opposites.ContainsKey(inferredDirection)
                    && opposites[inferredDirection] == heuristicContext.CurrentMove
                )
                {
                    return -1.2m;
                }
            }
        }

        return 0m;
    }
}
