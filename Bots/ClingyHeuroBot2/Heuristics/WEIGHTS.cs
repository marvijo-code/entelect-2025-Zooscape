namespace ClingyHeuroBot2.Heuristics;

using System.Collections.Generic;

public static class WEIGHTS
{
    public const decimal DistanceToGoal = 1.0m;
    public const decimal OpponentProximity = -0.8m;
    public const decimal ResourceClustering = 600.0m;
    public const decimal AreaControl = 0.7m;
    public const decimal Mobility = 50.0m;
    public const decimal PathSafety = -0.9m;

    public const decimal ReverseMovePenalty = -1.5m;

    public const decimal UnexploredBonus = 1.2m;

    public const decimal VisitPenalty = -100.0m;

    public const decimal UnexploredQuadrantBonus = 2.0m;

    public const decimal ZookeeperPrediction = -1.2m;
    public const decimal CaptureAvoidance = 1.6m;
    public const decimal SpawnProximity = -0.6m;
    public const decimal TimeToCapture = -0.6m;
    public const decimal EdgeSafety = -0.3m;
    public const decimal QuadrantAwareness = 0.5m;
    public const decimal TargetEvaluation = 1.5m;
    public const decimal TiebreakersAwareness = 0.7m;
    public const decimal ZookeeperCooldown = 0.9m;
    public const decimal PelletEfficiency = 1.1m;
    public const decimal EscapeRoutes = 0.8m;
    public const decimal AnimalCongestion = -0.6m;
    public const decimal CaptureRecoveryStrategy = 1.3m;

    public const decimal UnexploredBonusHeuristicWeight = 1.0m; // Weight for the UnexploredBonusHeuristic

    // New winning heuristic weights
    public const decimal FirstCommandAdvantage = 1.8m;
    public const decimal TravelDistanceMaximizer = 0.6m;
    public const decimal SpawnTimeMinimizer = 2.0m;
    public const decimal TimerAwareness = 1.2m;
    public const decimal PelletRatioAwareness = 1.4m;
    public const decimal CommandQueueOptimization = 0.9m;
    public const decimal AnticipateCompetition = 1.0m;
    public const decimal EndgameStrategy = 2.5m;
    public const decimal PositionalDominance = 1.7m;
    public const decimal ScoreLossMinimizer = 1.3m;

    // Anti-cycling heuristic weights
    public const decimal CycleDetection = 2.0m;
    public const decimal DirectionalVariety = 1.2m;
    public const decimal EmptyCellAvoidance = 1.6m;

    // >>> NEW: Heuristic weights for Zooscape 2025 rules
    public const decimal WallCollisionRisk = 1.0m;
    public const decimal LineOfSightPellets = 50.0m;
    public const decimal PelletRace = 1.1m;
    public const decimal RecalcWindowSafety = 0.8m;
    public const decimal CenterControl = 0.6m;

    public const decimal PowerUpCollection = 2.5m;
    public const decimal UseItem = 2.0m;
    public const decimal ScoreStreak = 0.2m;

    public const decimal MoveIfIdle = 3.0m;
    public const decimal ChangeDirectionWhenStuck = 2.5m;
    public const decimal ShortestPathToGoal = 1.8m;
    public const decimal EdgeAwareness = 0.7m;
    public const decimal UnoccupiedCellBonus = 1.0m;
    public const decimal OpponentTrailChasing = 0.5m;
    public const decimal CenterDistanceBonus = 0.4m;
    public const decimal MovementConsistency = 0.8m;
    public const decimal TunnelNavigation = 1.2m;
    public const decimal EarlyGameZookeeperAvoidance = 4.0m;

    public const decimal PelletAreaControl = 1.6m;
    public const decimal DensityMapping = 1.3m;
    public const decimal CornerControl = 0.9m;
    public const decimal AdaptivePathfinding = 1.1m;

    public static Dictionary<string, decimal> GetWeights()
    {
        return new Dictionary<string, decimal>
        {
            { "DistanceToGoal", DistanceToGoal },
            { "OpponentProximity", OpponentProximity },
            { "ResourceClustering", ResourceClustering },
            { "AreaControl", AreaControl },
            { "Mobility", Mobility },
            { "PathSafety", PathSafety },
            { "ReverseMovePenalty", ReverseMovePenalty },
            { "UnexploredBonus", UnexploredBonusHeuristicWeight },
            { "VisitPenalty", VisitPenalty },
            { "UnexploredQuadrantBonus", UnexploredQuadrantBonus },
            { "ZookeeperPrediction", ZookeeperPrediction },
            { "CaptureAvoidance", CaptureAvoidance },
            { "SpawnProximity", SpawnProximity },
            { "TimeToCapture", TimeToCapture },
            { "EdgeSafety", EdgeSafety },
            { "QuadrantAwareness", QuadrantAwareness },
            { "TargetEvaluation", TargetEvaluation },
            { "TiebreakersAwareness", TiebreakersAwareness },
            { "ZookeeperCooldown", ZookeeperCooldown },
            { "PelletEfficiency", PelletEfficiency },
            { "EscapeRoutes", EscapeRoutes },
            { "AnimalCongestion", AnimalCongestion },
            { "CaptureRecoveryStrategy", CaptureRecoveryStrategy },
            { "FirstCommandAdvantage", FirstCommandAdvantage },
            { "TravelDistanceMaximizer", TravelDistanceMaximizer },
            { "SpawnTimeMinimizer", SpawnTimeMinimizer },
            { "TimerAwareness", TimerAwareness },
            { "PelletRatioAwareness", PelletRatioAwareness },
            { "CommandQueueOptimization", CommandQueueOptimization },
            { "AnticipateCompetition", AnticipateCompetition },
            { "EndgameStrategy", EndgameStrategy },
            { "PositionalDominance", PositionalDominance },
            { "ScoreLossMinimizer", ScoreLossMinimizer },
            { "CycleDetection", CycleDetection },
            { "DirectionalVariety", DirectionalVariety },
            { "EmptyCellAvoidance", EmptyCellAvoidance },
            { "WallCollisionRisk", WallCollisionRisk },
            { "LineOfSightPellets", LineOfSightPellets },
            { "PelletRace", PelletRace },
            { "RecalcWindowSafety", RecalcWindowSafety },
            { "CenterControl", CenterControl },
            { "MoveIfIdle", MoveIfIdle },
            { "ChangeDirectionWhenStuck", ChangeDirectionWhenStuck },
            { "ShortestPathToGoal", ShortestPathToGoal },
            { "EdgeAwareness", EdgeAwareness },
            { "UnoccupiedCellBonus", UnoccupiedCellBonus },
            { "OpponentTrailChasing", OpponentTrailChasing },
            { "CenterDistanceBonus", CenterDistanceBonus },
            { "MovementConsistency", MovementConsistency },
            { "TunnelNavigation", TunnelNavigation },
            { "EarlyGameZookeeperAvoidance", EarlyGameZookeeperAvoidance },
            { "PelletAreaControl", PelletAreaControl },
            { "DensityMapping", DensityMapping },
            { "CornerControl", CornerControl },
            { "AdaptivePathfinding", AdaptivePathfinding },
            { "PowerUpCollection", PowerUpCollection },
            { "UseItem", UseItem },
            { "ScoreStreak", ScoreStreak },
        };
    }
}
