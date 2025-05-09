using HeuroBot.Enums;
using HeuroBot.Models;

namespace HeuroBot.Services;

public static class Heuristics
{
    public static decimal ScoreMove(GameState state, BotAction move)
    {
        decimal score = 0m;
        score += HeuristicsImpl.DistanceToGoal(state, move) * WEIGHTS.DistanceToGoal;
        score += HeuristicsImpl.OpponentProximity(state, move) * WEIGHTS.OpponentProximity;
        score += HeuristicsImpl.ResourceClustering(state, move) * WEIGHTS.ResourceClustering;
        score += HeuristicsImpl.AreaControl(state, move) * WEIGHTS.AreaControl;
        score += HeuristicsImpl.Mobility(state, move) * WEIGHTS.Mobility;
        score += HeuristicsImpl.PathSafety(state, move) * WEIGHTS.PathSafety;
        return score;
    }

    static class HeuristicsImpl
    {
        public static decimal DistanceToGoal(GameState state, BotAction m) => 0m; // TODO

        public static decimal OpponentProximity(GameState state, BotAction m) => 0m; // TODO

        public static decimal ResourceClustering(GameState state, BotAction m) => 0m; // TODO

        public static decimal AreaControl(GameState state, BotAction m) => 0m; // TODO

        public static decimal Mobility(GameState state, BotAction m) => 0m; // TODO

        public static decimal PathSafety(GameState state, BotAction m) => 0m; // TODO
    }
}
