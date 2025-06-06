#pragma warning disable SKEXP0110 // Added
using System;
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils; // Added
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class EscapeRoutesHeuristic : IHeuristic
{
    public string Name => "EscapeRoutes";

    public decimal CalculateRawScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition; // Updated

        int escapeCount = 0;
        foreach (BotAction action in Enum.GetValues<BotAction>())
        {
            var (ex, ey) = BotUtils.ApplyMove(nx, ny, action); // Updated
            if (BotUtils.IsTraversable(heuristicContext.CurrentGameState, ex, ey)) // Updated
            {
                if (heuristicContext.CurrentGameState.Zookeepers.Any())
                {
                    var zookeeper = heuristicContext
                        .CurrentGameState.Zookeepers.OrderBy(z =>
                            BotUtils.ManhattanDistance(z.X, z.Y, nx, ny)
                        )
                        .First();

                    int currentDist = BotUtils.ManhattanDistance( // Updated
                        zookeeper.X,
                        zookeeper.Y,
                        nx,
                        ny
                    );
                    int escapeDist = BotUtils.ManhattanDistance(zookeeper.X, zookeeper.Y, ex, ey); // Updated

                    if (escapeDist > currentDist)
                        escapeCount++;
                }
                else
                {
                    escapeCount++;
                }
            }
        }

        return escapeCount * 0.3m;
    }
}
