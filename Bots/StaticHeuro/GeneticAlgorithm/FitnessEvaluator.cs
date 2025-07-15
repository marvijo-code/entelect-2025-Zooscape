#pragma warning disable SKEXP0110

namespace StaticHeuro.GeneticAlgorithm;

/// <summary>
/// Evaluates and calculates fitness for bot individuals based on game performance
/// </summary>
public class FitnessEvaluator
{
    public FitnessConfig Config { get; set; }

    public FitnessEvaluator(FitnessConfig? config = null)
    {
        Config = config ?? new FitnessConfig();
    }

    /// <summary>
    /// Calculates comprehensive fitness for a game performance
    /// </summary>
    public double CalculateFitness(GamePerformance performance)
    {
        var components = new Dictionary<string, double>
        {
            ["Score"] = CalculateScoreComponent(performance),
            ["Survival"] = CalculateSurvivalComponent(performance),
            ["CaptureAvoidance"] = CalculateCaptureAvoidanceComponent(performance),
            ["Efficiency"] = CalculateEfficiencyComponent(performance),
            ["Rank"] = CalculateRankComponent(performance),
            ["Consistency"] = CalculateConsistencyComponent(performance),
            ["Progress"] = CalculateProgressComponent(performance)
        };

        // Calculate weighted sum
        double totalFitness = 0;
        foreach (var component in components)
        {
            var weight = GetComponentWeight(component.Key);
            totalFitness += component.Value * weight;
            
            // Store detailed metrics
            performance.DetailedMetrics[$"{component.Key}Component"] = component.Value;
            performance.DetailedMetrics[$"{component.Key}Weight"] = weight;
            performance.DetailedMetrics[$"{component.Key}Contribution"] = component.Value * weight;
        }

        // Apply normalization and bonuses
        totalFitness = ApplyNormalizationAndBonuses(totalFitness, performance, components);

        return Math.Max(0, totalFitness);
    }

    /// <summary>
    /// Calculates fitness based on raw game score
    /// </summary>
    private double CalculateScoreComponent(GamePerformance performance)
    {
        // Linear score component with diminishing returns for very high scores
        var baseScore = performance.Score * Config.ScoreMultiplier;
        
        if (performance.Score > Config.ScoreThreshold)
        {
            var excess = performance.Score - Config.ScoreThreshold;
            baseScore = Config.ScoreThreshold * Config.ScoreMultiplier + 
                       excess * Config.ScoreMultiplier * Config.DiminishingReturnsRate;
        }

        return baseScore;
    }

    /// <summary>
    /// Calculates fitness based on survival time
    /// </summary>
    private double CalculateSurvivalComponent(GamePerformance performance)
    {
        var survivalSeconds = performance.SurvivalTime.TotalSeconds;
        var gameDurationSeconds = performance.GameDuration.TotalSeconds;
        
        if (gameDurationSeconds <= 0) return 0;

        var survivalRatio = survivalSeconds / gameDurationSeconds;
        return survivalRatio * Config.SurvivalMultiplier;
    }

    /// <summary>
    /// Calculates fitness based on capture avoidance
    /// </summary>
    private double CalculateCaptureAvoidanceComponent(GamePerformance performance)
    {
        if (performance.TimesCaptured == 0)
            return Config.NoCaptureBonus;

        // Exponential penalty for captures
        var penalty = Math.Pow(performance.TimesCaptured, Config.CapturePenaltyExponent) * Config.CapturePenaltyMultiplier;
        return Math.Max(0, Config.NoCaptureBonus - penalty);
    }

    /// <summary>
    /// Calculates fitness based on movement efficiency
    /// </summary>
    private double CalculateEfficiencyComponent(GamePerformance performance)
    {
        if (performance.MovesExecuted <= 0) return 0;

        // Score per move (efficiency)
        var efficiency = (double)performance.Score / performance.MovesExecuted;
        
        // Distance efficiency (score per distance traveled)
        var distanceEfficiency = performance.DistanceTraveled > 0 
            ? performance.Score / performance.DistanceTraveled 
            : 0;

        var combinedEfficiency = (efficiency + distanceEfficiency) / 2;
        return combinedEfficiency * Config.EfficiencyMultiplier;
    }

    /// <summary>
    /// Calculates fitness based on final ranking
    /// </summary>
    private double CalculateRankComponent(GamePerformance performance)
    {
        if (performance.TotalPlayers <= 1) return 0;

        var rankFromBottom = performance.TotalPlayers - performance.FinalRank + 1;
        var rankRatio = (double)rankFromBottom / performance.TotalPlayers;
        
        return rankRatio * Config.RankMultiplier;
    }

    /// <summary>
    /// Calculates fitness based on consistency metrics
    /// </summary>
    private double CalculateConsistencyComponent(GamePerformance performance)
    {
        // Consistency is measured by how close performance is to expected values
        var consistencyScore = 0.0;

        // Pellet collection consistency
        var expectedPellets = Math.Max(1, performance.SurvivalTime.TotalSeconds / 10); // Expect 1 pellet per 10 seconds
        var pelletRatio = Math.Min(1.0, performance.PelletsCollected / expectedPellets);
        consistencyScore += pelletRatio * 0.5;

        // Movement consistency
        if (performance.MovesExecuted > 0)
        {
            var expectedMoves = performance.SurvivalTime.TotalSeconds; // Expect ~1 move per second
            var moveRatio = Math.Min(1.0, performance.MovesExecuted / expectedMoves);
            consistencyScore += moveRatio * 0.5;
        }

        return consistencyScore * Config.ConsistencyMultiplier;
    }

    /// <summary>
    /// Calculates fitness based on progress toward goals
    /// </summary>
    private double CalculateProgressComponent(GamePerformance performance)
    {
        // Progress measured by average distance to goals
        if (performance.AverageDistanceToGoal <= 0) return Config.ProgressMultiplier;

        // Inverse relationship - closer to goals is better
        var progressScore = 1.0 / (1.0 + performance.AverageDistanceToGoal / 100.0);
        return progressScore * Config.ProgressMultiplier;
    }

    /// <summary>
    /// Gets the weight for a specific fitness component
    /// </summary>
    private double GetComponentWeight(string componentName)
    {
        return componentName switch
        {
            "Score" => Config.ScoreWeight,
            "Survival" => Config.SurvivalWeight,
            "CaptureAvoidance" => Config.CaptureAvoidanceWeight,
            "Efficiency" => Config.EfficiencyWeight,
            "Rank" => Config.RankWeight,
            "Consistency" => Config.ConsistencyWeight,
            "Progress" => Config.ProgressWeight,
            _ => 1.0
        };
    }

    /// <summary>
    /// Applies normalization and special bonuses to the fitness score
    /// </summary>
    private double ApplyNormalizationAndBonuses(double baseFitness, GamePerformance performance, 
        Dictionary<string, double> components)
    {
        var adjustedFitness = baseFitness;

        // Perfect game bonus (no captures, high score)
        if (performance.TimesCaptured == 0 && performance.Score > Config.HighScoreThreshold)
        {
            adjustedFitness += Config.PerfectGameBonus;
        }

        // Long survival bonus
        if (performance.SurvivalTime.TotalMinutes > Config.LongSurvivalThresholdMinutes)
        {
            adjustedFitness += Config.LongSurvivalBonus;
        }

        // High efficiency bonus
        if (performance.EfficiencyRatio > Config.HighEfficiencyThreshold)
        {
            adjustedFitness += Config.HighEfficiencyBonus;
        }

        // First place bonus
        if (performance.FinalRank == 1)
        {
            adjustedFitness += Config.FirstPlaceBonus;
        }

        // Apply game mode multipliers
        adjustedFitness *= GetGameModeMultiplier(performance.GameMode);

        return adjustedFitness;
    }

    /// <summary>
    /// Gets multiplier based on game mode difficulty
    /// </summary>
    private double GetGameModeMultiplier(string gameMode)
    {
        return gameMode?.ToLower() switch
        {
            "easy" => 0.8,
            "normal" or "standard" => 1.0,
            "hard" => 1.2,
            "expert" => 1.5,
            "tournament" => 1.3,
            _ => 1.0
        };
    }

    /// <summary>
    /// Evaluates multiple games and calculates overall fitness for an individual
    /// </summary>
    public double EvaluateIndividualFitness(Individual individual)
    {
        if (!individual.PerformanceHistory.Any())
            return 0;

        var performances = individual.PerformanceHistory.ToList();
        
        // Calculate fitness for each performance if not already calculated
        foreach (var performance in performances.Where(p => p.Fitness <= 0))
        {
            performance.Fitness = CalculateFitness(performance);
        }

        // Use weighted average with recent games having more impact
        return CalculateWeightedAverageFitness(performances);
    }

    /// <summary>
    /// Calculates weighted average fitness with recency bias
    /// </summary>
    private double CalculateWeightedAverageFitness(List<GamePerformance> performances)
    {
        if (!performances.Any()) return 0;

        var sortedPerformances = performances.OrderBy(p => p.GameEndTime).ToList();
        double weightedSum = 0;
        double totalWeight = 0;

        for (int i = 0; i < sortedPerformances.Count; i++)
        {
            // More recent games get exponentially higher weights
            double weight = Math.Pow(Config.RecencyBias, sortedPerformances.Count - i - 1);
            weightedSum += sortedPerformances[i].Fitness * weight;
            totalWeight += weight;
        }

        return totalWeight > 0 ? weightedSum / totalWeight : 0;
    }

    /// <summary>
    /// Creates a detailed fitness report for an individual
    /// </summary>
    public FitnessReport CreateFitnessReport(Individual individual)
    {
        var report = new FitnessReport
        {
            IndividualId = individual.Id,
            OverallFitness = individual.Fitness,
            GamesEvaluated = individual.GamesPlayed,
            AverageFitness = individual.AverageFitness,
            BestGameFitness = individual.BestFitness,
            ComponentBreakdown = new Dictionary<string, double>(),
            RecentTrend = CalculateFitnessTrend(individual.PerformanceHistory.TakeLast(5).ToList())
        };

        // Calculate average component contributions
        if (individual.PerformanceHistory.Any())
        {
            var components = new[] { "Score", "Survival", "CaptureAvoidance", "Efficiency", "Rank", "Consistency", "Progress" };
            foreach (var component in components)
            {
                var avgContribution = individual.PerformanceHistory
                    .Where(p => p.DetailedMetrics.ContainsKey($"{component}Contribution"))
                    .Average(p => p.DetailedMetrics[$"{component}Contribution"]);
                
                report.ComponentBreakdown[component] = avgContribution;
            }
        }

        return report;
    }

    /// <summary>
    /// Calculates the trend direction of recent fitness scores
    /// </summary>
    private double CalculateFitnessTrend(List<GamePerformance> recentPerformances)
    {
        if (recentPerformances.Count < 2) return 0;

        var fitnessValues = recentPerformances.Select(p => p.Fitness).ToList();
        
        // Simple linear regression slope
        var n = fitnessValues.Count;
        var sumX = n * (n + 1) / 2; // Sum of indices 1, 2, ..., n
        var sumY = fitnessValues.Sum();
        var sumXY = fitnessValues.Select((y, i) => (i + 1) * y).Sum();
        var sumXX = n * (n + 1) * (2 * n + 1) / 6; // Sum of squares 1^2 + 2^2 + ... + n^2

        var slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
        return slope;
    }
}

/// <summary>
/// Configuration for fitness evaluation parameters
/// </summary>
public class FitnessConfig
{
    // Component weights
    public double ScoreWeight { get; set; } = 1.0;
    public double SurvivalWeight { get; set; } = 0.3;
    public double CaptureAvoidanceWeight { get; set; } = 0.5;
    public double EfficiencyWeight { get; set; } = 0.4;
    public double RankWeight { get; set; } = 0.6;
    public double ConsistencyWeight { get; set; } = 0.2;
    public double ProgressWeight { get; set; } = 0.3;

    // Score component parameters
    public double ScoreMultiplier { get; set; } = 1.0;
    public int ScoreThreshold { get; set; } = 1000;
    public double DiminishingReturnsRate { get; set; } = 0.5;

    // Survival component parameters
    public double SurvivalMultiplier { get; set; } = 100.0;

    // Capture avoidance parameters
    public double NoCaptureBonus { get; set; } = 50.0;
    public double CapturePenaltyMultiplier { get; set; } = 25.0;
    public double CapturePenaltyExponent { get; set; } = 1.5;

    // Efficiency parameters
    public double EfficiencyMultiplier { get; set; } = 10.0;

    // Rank parameters
    public double RankMultiplier { get; set; } = 30.0;

    // Consistency parameters
    public double ConsistencyMultiplier { get; set; } = 20.0;

    // Progress parameters
    public double ProgressMultiplier { get; set; } = 15.0;

    // Bonus thresholds and values
    public int HighScoreThreshold { get; set; } = 2000;
    public double LongSurvivalThresholdMinutes { get; set; } = 5.0;
    public double HighEfficiencyThreshold { get; set; } = 10.0;
    public double PerfectGameBonus { get; set; } = 100.0;
    public double LongSurvivalBonus { get; set; } = 50.0;
    public double HighEfficiencyBonus { get; set; } = 30.0;
    public double FirstPlaceBonus { get; set; } = 75.0;

    // Fitness calculation parameters
    public double RecencyBias { get; set; } = 1.1; // How much more recent games matter
}

/// <summary>
/// Detailed fitness evaluation report
/// </summary>
public class FitnessReport
{
    public Guid IndividualId { get; set; }
    public double OverallFitness { get; set; }
    public int GamesEvaluated { get; set; }
    public double AverageFitness { get; set; }
    public double BestGameFitness { get; set; }
    public Dictionary<string, double> ComponentBreakdown { get; set; } = new();
    public double RecentTrend { get; set; } // Positive = improving, negative = declining

    public override string ToString()
    {
        var trendText = RecentTrend > 0.1 ? "↗ Improving" : 
                       RecentTrend < -0.1 ? "↘ Declining" : 
                       "→ Stable";

        return $"Fitness Report for {IndividualId:D}\n" +
               $"Overall: {OverallFitness:F2} | Avg: {AverageFitness:F2} | Best: {BestGameFitness:F2}\n" +
               $"Games: {GamesEvaluated} | Trend: {trendText} ({RecentTrend:F3})";
    }
} 