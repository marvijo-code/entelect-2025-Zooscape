using MCTSo4.Enums;
using MCTSo4.Models;

namespace MCTSo4.Algorithms.MCTS
{
    public static class AdaptiveStrategyController
    {
        public static MetaStrategy DetermineCurrentMetaStrategy(MCTSGameState state)
        {
            // TODO: Analyze state and choose mode
            return MetaStrategy.Collecting;
        }

        public static BotParameters ConfigureParameters(MetaStrategy strategy)
        {
            return new BotParameters { Strategy = strategy };
        }
    }
}
