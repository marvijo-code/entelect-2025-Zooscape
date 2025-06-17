namespace Marvijo.Zooscape.Bots.Common.Models;

public class HeuristicWeights
{
    public decimal DistanceToGoal { get; set; }
    public decimal OpponentProximity { get; set; }
    public decimal ResourceClustering { get; set; }
    public decimal AreaControl { get; set; }
    public decimal Mobility { get; set; }
    public decimal PathSafety { get; set; }
    public decimal ReverseMovePenalty { get; set; }
    public decimal UnexploredBonus { get; set; }
    public decimal VisitPenalty { get; set; }
    public decimal UnexploredQuadrantBonus { get; set; }
    public decimal ZookeeperPrediction { get; set; }
    public decimal CaptureAvoidance { get; set; }
    public decimal SpawnProximity { get; set; }
    public decimal TimeToCapture { get; set; }
    public decimal EdgeSafety { get; set; }
    public decimal QuadrantAwareness { get; set; }
    public decimal TargetEvaluation { get; set; }
    public decimal TiebreakersAwareness { get; set; }
    public decimal ZookeeperCooldown { get; set; }
    public decimal PelletEfficiency { get; set; }
    public decimal EscapeRoutes { get; set; }
    public decimal AnimalCongestion { get; set; }
    public decimal CaptureRecoveryStrategy { get; set; }
    public decimal FirstCommandAdvantage { get; set; }
    public decimal TravelDistanceMaximizer { get; set; }
    public decimal SpawnTimeMinimizer { get; set; }
    public decimal TimerAwareness { get; set; }
    public decimal PelletRatioAwareness { get; set; }
    public decimal CommandQueueOptimization { get; set; }
    public decimal AnticipateCompetition { get; set; }
    public decimal EndgameStrategy { get; set; }
    public decimal PositionalDominance { get; set; }
    public decimal ScoreLossMinimizer { get; set; }
    public decimal CycleDetection { get; set; }
    public decimal DirectionalVariety { get; set; }
    public decimal EmptyCellAvoidance { get; set; }
    public decimal WallCollisionRisk { get; set; }
    public decimal LineOfSightPellets { get; set; }
    public decimal PelletRace { get; set; }
    public decimal RecalcWindowSafety { get; set; }
    public decimal CenterControl { get; set; }
    public decimal MoveIfIdle { get; set; }
    public decimal ChangeDirectionWhenStuck { get; set; }
    public decimal ShortestPathToGoal { get; set; }
    public decimal EdgeAwareness { get; set; }
    public decimal UnoccupiedCellBonus { get; set; }
    public decimal OpponentTrailChasing { get; set; }
    public decimal CenterDistanceBonus { get; set; }
    public decimal MovementConsistency { get; set; }
    public decimal TunnelNavigation { get; set; }
    public decimal EarlyGameZookeeperAvoidance { get; set; }
    public decimal PelletAreaControl { get; set; }
    public decimal DensityMapping { get; set; }
    public decimal CornerControl { get; set; }
    public decimal AdaptivePathfinding { get; set; }
    public decimal PowerUpCollection { get; set; }
    public decimal UseItem { get; set; }
    public decimal ScoreStreak { get; set; }

    // RecalcWindowSafetyHeuristic
    public int RecalcWindowSize { get; set; }
    public int RecalcWindowSafetyTickThreshold { get; set; }
    public int RecalcWindowSafetyDistanceThreshold { get; set; }
    public decimal RecalcWindowSafetyPenalty { get; set; }
    public decimal RecalcWindowSafetyBonus { get; set; }

    // ZookeeperCooldownHeuristic
    public int ZookeeperCooldownRecalcInterval { get; set; }
    public decimal ZookeeperCooldownBonus { get; set; }

    // SpawnProximityHeuristic
    public int SpawnProximityDistanceThreshold { get; set; }
    public decimal SpawnProximityPenalty { get; set; }
    public decimal SpawnProximityBonus { get; set; }

    // UseItemHeuristic
    public decimal UseItemBonus { get; set; }

    // OpponentTrailChasingHeuristic
    public int OpponentTrailChasingMinDistance { get; set; }
    public int OpponentTrailChasingMaxDistance { get; set; }
    public decimal OpponentTrailChasingBonus { get; set; }

    // ZookeeperPredictionHeuristic
    public decimal ZookeeperPredictionPenalty { get; set; }
}
