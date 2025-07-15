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
    public decimal CaptureAvoidancePenaltyFactor { get; set; }
    public decimal CaptureAvoidanceRewardFactor { get; set; }
    public decimal SpawnProximity { get; set; }
    public decimal TimeToCapture { get; set; }
    public decimal EdgeSafety { get; set; }
    public decimal QuadrantAwareness { get; set; }
    public decimal TargetEvaluation { get; set; }
    public decimal TiebreakersAwareness { get; set; }
    public decimal ZookeeperCooldown { get; set; }
    public decimal PelletEfficiency { get; set; }
    public decimal LongTermPelletSeekingFactor { get; set; }
    public decimal ImmediatePelletBonus { get; set; }
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

    // Properties added based on build errors in ClingyHeuroBot2
    public decimal CapturePenaltyPercent { get; set; }
    public decimal ScoreLossMinimizerHighRiskDistance { get; set; }
    public decimal ScoreLossMinimizerHighRiskFactor { get; set; }
    public decimal ScoreLossMinimizerRiskDistanceDivisor { get; set; }
    public decimal ScoreLossMinimizerMediumRiskDistance { get; set; }
    public decimal ScoreLossMinimizerMediumRiskFactor { get; set; }
    public decimal ScoreLossMinimizerSignificantScoreThreshold { get; set; }
    public decimal ScoreLossMinimizerCautionFactor { get; set; }
    public decimal ScoreLossMinimizerLowScoreCautionFactor { get; set; }
    public decimal TiebreakerScore { get; set; }
    public decimal TiebreakerDistance { get; set; }
    public decimal TiebreakerCaptured { get; set; }
    public decimal TimeToCaptureDanger { get; set; }
    public decimal TimeToCaptureSafety { get; set; }
    public decimal QuadrantPelletBonus { get; set; }
    public decimal QuadrantAnimalPenalty { get; set; }
    public decimal LongTermPelletSeeking { get; set; }

    // Properties added based on further build errors in ClingyHeuroBot2 (Batch 2)
    public decimal PathSafetyPenalty { get; set; }
    public decimal MovementConsistencyBonus { get; set; }
    public decimal MovementConsistencyPenalty { get; set; }
    public decimal WallCollisionPenaltyImmediate { get; set; }
    public decimal WallCollisionPenaltyNear { get; set; }
    public decimal WallCollisionPenaltyMidRange { get; set; }
    public int SpawnProximityEarlyGameDistanceThreshold { get; set; }
    public int SpawnProximityEarlyGameTickThreshold { get; set; }
    public decimal SpawnProximityEarlyGamePenalty { get; set; }
    public decimal SpawnProximityDistanceBonusDivisor { get; set; }
    public decimal ScoreStreakBonus { get; set; }
    public int OpponentChaseMinDistance { get; set; }
    public int OpponentChaseMaxDistance { get; set; }
    public decimal OpponentChaseBonus { get; set; }
    public decimal ResourceClusteringBonus { get; set; }
    public decimal ResourceClusteringImmediatePelletBonus { get; set; }

    // Properties added based on further build errors in ClingyHeuroBot2 (Batch 3)
    public decimal ZookeeperPredictedPositionPenalty { get; set; }
    public int ZookeeperNearPredictedPositionDistance { get; set; }
    public decimal ZookeeperNearPredictedPositionPenalty { get; set; }
    public decimal ZookeeperNearPredictedPositionDivisor { get; set; }
    public decimal EarlyGameZookeeperAvoidancePenalty { get; set; }
    public decimal EdgeSafetyPenalty_0 { get; set; }
    public decimal EdgeSafetyPenalty_1 { get; set; }
    public decimal EdgeSafetyPenalty_2 { get; set; }

    // OpponentTrailChasingHeuristic
    public int OpponentTrailChasingMinDistance { get; set; }
    public int OpponentTrailChasingMaxDistance { get; set; }
    public decimal OpponentTrailChasingBonus { get; set; }

    // ZookeeperPredictionHeuristic
    public decimal ZookeeperPredictionPenalty { get; set; }
}
