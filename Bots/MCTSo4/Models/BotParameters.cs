using MCTSo4.Enums;

namespace MCTSo4.Models
{
    /// <summary>
    /// Configuration container for MCTS hyperparameters and heuristic weights per meta-strategy.
    /// </summary>
    public class BotParameters
    {
        public MetaStrategy Strategy { get; set; }
        public int MctsIterations { get; set; } = 100;
        public int MctsDepth { get; set; } = 10;
        public double ExplorationConstant { get; set; } = 1.41;

        /// <summary>
        /// Maximum time in milliseconds allowed for MCTS to run per move.
        /// If set to 0 or negative, the default time limit will be used.
        /// </summary>
        public int MaxTimePerMoveMs { get; set; } = 140;

        /// <summary>
        /// Progressive widening parameter - controls how many nodes to expand based on visit count.
        /// </summary>
        public double ProgressiveWideningBase { get; set; } = 2.0;

        /// <summary>
        /// Progressive widening exponent - controls rate of expansion.
        /// </summary>
        public double ProgressiveWideningExponent { get; set; } = 0.5;

        /// <summary>
        /// Virtual loss count to apply during parallel search.
        /// </summary>
        public int VirtualLossCount { get; set; } = 3;

        /// <summary>
        /// Weight factor for RAVE: 0 = disabled, 1 = full RAVE.
        /// </summary>
        public double RaveWeight { get; set; } = 0.5;

        /// <summary>
        /// RAVE equivalence parameter - controls how RAVE influence decreases with node visits.
        /// Higher values mean RAVE estimates stay influential longer.
        /// </summary>
        public double RaveEquivalenceParameter { get; set; } = 1000;

        /// <summary>
        /// Maximum depth to track moves for RAVE updates.
        /// </summary>
        public int RaveMaxDepth { get; set; } = 10;

        // Heuristic weights
        public double Weight_PelletValue { get; set; } = 10.0;
        public double Weight_ZkThreat { get; set; } = -100.0;
        public double Weight_EscapeProgress { get; set; } = 50.0;
        public double Weight_PowerUp { get; set; } = 20.0;
        public double Weight_OpponentContention { get; set; } = -5.0;
    }
}
