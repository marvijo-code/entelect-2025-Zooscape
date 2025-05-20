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
            // Increase parameters to account for improved simulation with zookeepers
            var parameters = new BotParameters
            {
                Strategy = strategy,
                MctsIterations = 600, // Increased from 500
                MctsDepth = 25, // Increased from 20 to account for zookeeper simulation
                ExplorationConstant = 1.41,

                // Tuned heuristic weights
                Weight_PelletValue = 15.0, // Increased from 10.0
                Weight_ZkThreat = -150.0, // More negative to increase zookeeper avoidance
                Weight_EscapeProgress = 50.0,
                Weight_PowerUp = 20.0,
                Weight_OpponentContention = -5.0,
            };

            // Adjust parameters based on strategy
            switch (strategy)
            {
                case MetaStrategy.Collecting:
                    // Default weights are already optimized for collecting
                    break;
                case MetaStrategy.Evading: // Changed from Escaping to Evading
                    parameters.Weight_ZkThreat = -200.0; // Even more negative when evading
                    parameters.Weight_EscapeProgress = 100.0; // Doubled
                    break;
                case MetaStrategy.EscapeFocus: // Changed from Survival to EscapeFocus
                    parameters.Weight_ZkThreat = -300.0; // Extremely negative
                    parameters.MctsDepth = 30; // Look further ahead
                    break;
                case MetaStrategy.PowerUpHunt:
                    parameters.Weight_PowerUp = 40.0; // Prioritize power-ups
                    break;
                case MetaStrategy.ZoneControl:
                    parameters.Weight_OpponentContention = -10.0; // Higher penalty for contested areas
                    break;
            }

            return parameters;
        }
    }
}
