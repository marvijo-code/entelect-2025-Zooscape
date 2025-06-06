using System;
using ClingyHeuroBot2;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2;

public class EscapeRoutesHeuristic : IHeuristic
{
    public string Name => "EscapeRoutes";

    public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
    {
        var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);

        int escapeCount = 0;
        foreach (BotAction action in Enum.GetValues<BotAction>())
        {
            var (ex, ey) = Heuristics.ApplyMove(nx, ny, action);
            if (Heuristics.IsTraversable(state, ex, ey))
            {
                if (state.Zookeepers.Any())
                {
                    var zookeeper = state
                        .Zookeepers.OrderBy(z => Heuristics.ManhattanDistance(z.X, z.Y, nx, ny))
                        .First();

                    int currentDist = Heuristics.ManhattanDistance(
                        zookeeper.X,
                        zookeeper.Y,
                        nx,
                        ny
                    );
                    int escapeDist = Heuristics.ManhattanDistance(zookeeper.X, zookeeper.Y, ex, ey);

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
