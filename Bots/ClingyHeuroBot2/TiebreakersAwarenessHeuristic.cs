// #pragma warning disable SKEXP0110 // Disable SK Experimental warning
using HeuroBot.Services; // For Heuristics.HeuristicsImpl static methods
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace HeuroBot.Bots.ClingyHeuroBot2.Heuristics;

public class TiebreakersAwarenessHeuristic : IHeuristic
{
    public string Name => "TiebreakersAwareness";

    public decimal CalculateRawScore(GameState state, Animal me, BotAction move, ILogger? logger)
    {
        var (nx, ny) = Heuristics.HeuristicsImpl.ApplyMove(me.X, me.Y, move);

        decimal value = 0m;

        // Encourage actual movement
        if (nx != me.X || ny != me.Y)
        {
            value += 0.1m;
        }

        // Strongly encourage moving off a spawn point
        if (me.X == me.SpawnX && me.Y == me.SpawnY)
        {
            value += 2.0m;
        }

        return value;
    }
}
