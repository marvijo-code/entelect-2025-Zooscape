using MCTSo4.Enums;
using MCTSo4.Models;

namespace MCTSo4.Algorithms.MCTS;

public static class AdaptiveStrategyController
{
    public static MetaStrategy DetermineCurrentMetaStrategy(MCTSGameState state)
    {
        // TODO: Analyze state and choose mode
        return MetaStrategy.Collecting;
    }

    public static BotParameters ConfigureParameters(MetaStrategy strategy)
    {
        // Base parameters optimized for parallel MCTS search
        var parameters = new BotParameters
        {
            Strategy = strategy,
            MctsIterations = 800, // Increased from 600 for parallel search
            MctsDepth = 25, // Deep simulation depth
            ExplorationConstant = 1.5, // Slightly higher exploration for better parallelization

            // Time budget of 130ms
            MaxTimePerMoveMs = 130,

            // Progressive widening parameters
            ProgressiveWideningBase = 2.0,
            ProgressiveWideningExponent = 0.5,

            // Virtual loss count for parallel search
            VirtualLossCount = 3,

            // RAVE parameters
            RaveWeight = 0.5,
            RaveEquivalenceParameter = 1000,
            RaveMaxDepth = 10,

            // Default heuristic weights (can be overridden by strategy)
            Weight_ScoreStreakBonus = 5.0, // Default bonus per streak level
        };

        // Strategy-specific parameter tuning
        switch (strategy)
        {
            case MetaStrategy.Collecting:
                parameters.Weight_PelletValue = 10.0; // High value on pellet collection
                parameters.Weight_ZkThreat = -80.0; // Moderate penalty for zookeeper threat
                parameters.Weight_EscapeProgress = 5.0; // Low emphasis on escape progress
                parameters.Weight_PowerUp = 30.0; // High value on power-ups
                parameters.Weight_OpponentContention = -5.0; // Moderate penalty for opponent contention

                // RAVE tuning for collecting (higher equivalence - rely on RAVE more)
                parameters.RaveWeight = 0.6;
                parameters.RaveEquivalenceParameter = 1500;
                break;

            case MetaStrategy.Evading:
                parameters.Weight_PelletValue = 5.0; // Lower value on pellet collection
                parameters.Weight_ZkThreat = -150.0; // Extreme penalty for zookeeper threat
                parameters.Weight_EscapeProgress = 20.0; // Moderate emphasis on escape progress
                parameters.Weight_PowerUp = 40.0; // Very high value on power-ups for escape
                parameters.Weight_OpponentContention = -2.0; // Low penalty for opponent contention

                // RAVE tuning for evading (less RAVE influence - more UCT)
                parameters.RaveWeight = 0.4;
                parameters.RaveEquivalenceParameter = 800;
                break;

            case MetaStrategy.EscapeFocus:
                parameters.Weight_PelletValue = 1.0; // Minimal value on pellet collection
                parameters.Weight_ZkThreat = -100.0; // High penalty for zookeeper threat
                parameters.Weight_EscapeProgress = 100.0; // Extreme emphasis on escape progress
                parameters.Weight_PowerUp = 20.0; // Moderate value on power-ups
                parameters.Weight_OpponentContention = -1.0; // Minimal penalty for opponent contention

                // RAVE tuning for escape focus (minimal RAVE influence - most UCT)
                parameters.RaveWeight = 0.3;
                parameters.RaveEquivalenceParameter = 500;
                break;
        }

        return parameters;
    }
}
