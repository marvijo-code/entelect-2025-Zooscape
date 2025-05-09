namespace HeuroBot
{
    public static class WEIGHTS
    {
        public const decimal PelletSeekingUrgency = 2.0m;
        public const decimal PelletClusterBonus = 0.8m;
        public const decimal UnexploredCellBonus = 0.5m;
        public const decimal ZookeeperRepulsionDirect = -5.0m;
        public const decimal ZookeeperProximityPenalty = -2.0m;
        public const decimal ZookeeperTargetedPenalty = -10.0m;
        public const decimal PathSafetyScore = 1.5m;
        public const decimal FutureMobility = 0.3m;
        public const decimal ReverseMovePenalty = -1.5m;
        public const decimal StrategicTerritoryControl = 0.4m;
        public const decimal MakeOpponentZookeeperTargetBonus = 0.5m;
        public const decimal CageExitUrgency = 10.0m;
        public const decimal AvoidOwnCagePenalty = -5.0m;
        public const decimal TicksSinceCaughtSurvivalBonus = 0.01m;
        public const decimal CriticalDangerEvasionMultiplier = 3.0m;
        public const decimal FewPelletsRemainingFocusMultiplier = 2.0m;
        public const decimal ZookeeperRecalculatingTargetSoonFactor = 1.2m;
    }
}
