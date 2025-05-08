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

        // Heuristic weights
        public double Weight_PelletValue { get; set; } = 10.0;
        public double Weight_ZkThreat { get; set; } = -100.0;
        public double Weight_EscapeProgress { get; set; } = 50.0;
        public double Weight_PowerUp { get; set; } = 20.0;
        public double Weight_OpponentContention { get; set; } = -5.0;
    }
}
