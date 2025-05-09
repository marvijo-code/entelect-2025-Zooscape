namespace HeuroBot;

public static class WEIGHTS
{
    public const decimal DistanceToGoal = 1.0m;
    public const decimal OpponentProximity = -0.8m;
    public const decimal ResourceClustering = 0.5m;
    public const decimal AreaControl = 0.7m;
    public const decimal Mobility = 0.3m;
    public const decimal PathSafety = -0.9m;

    public const decimal ReverseMovePenalty = -1.5m;

    public const decimal UnexploredBonus = 1.2m;

    public const decimal VisitPenalty = -0.4m;

    public const decimal UnexploredQuadrantBonus = 2.0m;

    public const decimal ZookeeperPrediction = -1.2m;
    public const decimal CaptureAvoidance = 1.6m;
    public const decimal SpawnProximity = -0.6m;
    public const decimal TimeToCapture = 0.8m;
    public const decimal EdgeSafety = -0.3m;
    public const decimal QuadrantAwareness = 0.5m;
    public const decimal TargetEvaluation = 1.5m;
    public const decimal TiebreakersAwareness = 0.7m;
    public const decimal ZookeeperCooldown = 0.9m;
    public const decimal PelletEfficiency = 1.1m;
    public const decimal EscapeRoutes = 0.8m;
    public const decimal AnimalCongestion = -0.6m;
    public const decimal CaptureRecoveryStrategy = 1.3m;
}
