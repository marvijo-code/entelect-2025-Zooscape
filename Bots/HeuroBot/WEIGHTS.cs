namespace HeuroBot;

public static class WEIGHTS
{
    public const decimal DistanceToGoal = 1.0m;
    public const decimal OpponentProximity = -0.8m;
    public const decimal ResourceClustering = 0.5m;
    public const decimal AreaControl = 0.7m;
    public const decimal Mobility = 0.3m;
    public const decimal PathSafety = -0.9m;

    // Penalty for reversing the previous move to avoid oscillation
    public const decimal ReverseMovePenalty = -1.5m;

    // Bonus for moving into unexplored cells
    public const decimal UnexploredBonus = 1.2m;

    // Penalty per visit to a cell (multiplied by visit count)
    public const decimal VisitPenalty = -0.4m;

    // Bonus for moving into unexplored quadrants
    public const decimal UnexploredQuadrantBonus = 2.0m;
}
