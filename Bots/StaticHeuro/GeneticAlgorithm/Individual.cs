#pragma warning disable SKEXP0110

using Marvijo.Zooscape.Bots.Common.Models;
using System.Text.Json;

namespace StaticHeuro.GeneticAlgorithm;

/// <summary>
/// Represents an individual bot in the genetic algorithm population.
/// Contains the genome (heuristic weights), fitness information, and performance history.
/// </summary>
public class Individual
{
    public Guid Id { get; set; }
    public HeuristicWeights Genome { get; set; }
    public double Fitness { get; set; }
    public int Generation { get; set; }
    public int Age { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastEvaluatedAt { get; set; }
    
    // Performance tracking
    public List<GamePerformance> PerformanceHistory { get; set; }
    public double AverageFitness => PerformanceHistory.Count > 0 ? PerformanceHistory.Average(p => p.Fitness) : 0;
    public double BestFitness => PerformanceHistory.Count > 0 ? PerformanceHistory.Max(p => p.Fitness) : 0;
    public int GamesPlayed => PerformanceHistory.Count;
    
    // Evolution metadata
    public List<Guid> ParentIds { get; set; }
    public string EvolutionMethod { get; set; } // "Random", "Crossover", "Mutation", etc.
    public Dictionary<string, object> Metadata { get; set; }

    public Individual()
    {
        Id = Guid.NewGuid();
        Genome = new HeuristicWeights();
        PerformanceHistory = new List<GamePerformance>();
        ParentIds = new List<Guid>();
        Metadata = new Dictionary<string, object>();
        CreatedAt = DateTime.UtcNow;
        EvolutionMethod = "Random";
    }

    public Individual(HeuristicWeights genome) : this()
    {
        Genome = genome ?? new HeuristicWeights();
    }

    public Individual(Individual parent1, Individual parent2, string evolutionMethod) : this()
    {
        ParentIds.Add(parent1.Id);
        ParentIds.Add(parent2.Id);
        Generation = Math.Max(parent1.Generation, parent2.Generation) + 1;
        EvolutionMethod = evolutionMethod;
    }

    /// <summary>
    /// Records performance from a completed game
    /// </summary>
    public void RecordPerformance(GamePerformance performance)
    {
        PerformanceHistory.Add(performance);
        LastEvaluatedAt = DateTime.UtcNow;
        
        // Update current fitness (could use different strategies here)
        Fitness = CalculateOverallFitness();
    }

    /// <summary>
    /// Calculates overall fitness based on performance history
    /// </summary>
    private double CalculateOverallFitness()
    {
        if (!PerformanceHistory.Any()) return 0;

        // Use weighted average with recent games having more impact
        var sortedPerformances = PerformanceHistory.OrderBy(p => p.GameEndTime).ToList();
        double weightedSum = 0;
        double totalWeight = 0;

        for (int i = 0; i < sortedPerformances.Count; i++)
        {
            // More recent games get exponentially higher weights
            double weight = Math.Pow(1.1, i);
            weightedSum += sortedPerformances[i].Fitness * weight;
            totalWeight += weight;
        }

        return totalWeight > 0 ? weightedSum / totalWeight : 0;
    }

    /// <summary>
    /// Creates a deep copy of this individual
    /// </summary>
    public Individual Clone()
    {
        var clone = new Individual
        {
            Id = Guid.NewGuid(), // New ID for the clone
            Genome = CloneGenome(),
            Fitness = Fitness,
            Generation = Generation,
            Age = Age,
            CreatedAt = DateTime.UtcNow,
            LastEvaluatedAt = LastEvaluatedAt,
            PerformanceHistory = new List<GamePerformance>(PerformanceHistory),
            ParentIds = new List<Guid>(ParentIds),
            EvolutionMethod = EvolutionMethod + "_Clone",
            Metadata = new Dictionary<string, object>(Metadata)
        };

        return clone;
    }

    /// <summary>
    /// Creates a deep copy of the genome
    /// </summary>
    private HeuristicWeights CloneGenome()
    {
        var json = JsonSerializer.Serialize(Genome);
        return JsonSerializer.Deserialize<HeuristicWeights>(json) ?? new HeuristicWeights();
    }

    /// <summary>
    /// Validates that all genome values are within acceptable bounds
    /// </summary>
    public bool IsValid()
    {
        try
        {
            var properties = typeof(HeuristicWeights).GetProperties()
                .Where(p => p.PropertyType == typeof(decimal) || p.PropertyType == typeof(int));

            foreach (var property in properties)
            {
                var value = property.GetValue(Genome);
                
                if (property.PropertyType == typeof(decimal))
                {
                    var decimalValue = (decimal)value;
                    // Check for reasonable bounds (adjust as needed)
                    if (decimalValue < -1000m || decimalValue > 1000m || !IsFinite(decimalValue))
                        return false;
                }
                else if (property.PropertyType == typeof(int))
                {
                    var intValue = (int)value;
                    // Check for reasonable bounds
                    if (intValue < 0 || intValue > 10000)
                        return false;
                }
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsFinite(decimal value)
    {
        return value != decimal.MaxValue && value != decimal.MinValue;
    }

    /// <summary>
    /// Gets a summary of this individual's performance
    /// </summary>
    public string GetPerformanceSummary()
    {
        return $"Individual {Id:D} - Gen:{Generation} Age:{Age} Games:{GamesPlayed} " +
               $"Fitness: Cur={Fitness:F3} Avg={AverageFitness:F3} Best={BestFitness:F3} " +
               $"Method:{EvolutionMethod}";
    }

    /// <summary>
    /// Saves this individual's genome to a JSON file
    /// </summary>
    public void SaveToFile(string filePath)
    {
        var data = new
        {
            Id,
            Fitness,
            Generation,
            Age,
            CreatedAt,
            LastEvaluatedAt,
            EvolutionMethod,
            Genome,
            PerformanceHistory = PerformanceHistory.Take(10).ToList(), // Save last 10 performances
            ParentIds,
            Metadata
        };

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        
        // Retry logic to handle file locking issues
        int maxRetries = 3;
        for (int retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                File.WriteAllText(filePath, json);
                return; // Success, exit retry loop
            }
            catch (IOException ex) when (retry < maxRetries - 1)
            {
                // File is locked, wait and retry
                Thread.Sleep(100 * (retry + 1)); // Increasing delay: 100ms, 200ms, 300ms
            }
            catch (IOException ex) when (retry == maxRetries - 1)
            {
                // Final retry failed, log error but don't crash
                Console.WriteLine($"Failed to save individual {Id} after {maxRetries} attempts: {ex.Message}");
                throw; // Re-throw on final attempt
            }
        }
    }

    /// <summary>
    /// Loads an individual from a JSON file
    /// </summary>
    public static Individual LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Individual file not found: {filePath}");

        var json = File.ReadAllText(filePath);
        var data = JsonSerializer.Deserialize<JsonElement>(json);

        var individual = new Individual
        {
            Id = data.GetProperty("Id").GetGuid(),
            Fitness = data.GetProperty("Fitness").GetDouble(),
            Generation = data.GetProperty("Generation").GetInt32(),
            Age = data.GetProperty("Age").GetInt32(),
            CreatedAt = data.GetProperty("CreatedAt").GetDateTime(),
            LastEvaluatedAt = data.GetProperty("LastEvaluatedAt").GetDateTime(),
            EvolutionMethod = data.GetProperty("EvolutionMethod").GetString() ?? "Unknown"
        };

        // Load genome
        var genomeJson = data.GetProperty("Genome").GetRawText();
        individual.Genome = JsonSerializer.Deserialize<HeuristicWeights>(genomeJson) ?? new HeuristicWeights();

        // Load performance history if present
        if (data.TryGetProperty("PerformanceHistory", out var perfHistory))
        {
            var perfJson = perfHistory.GetRawText();
            individual.PerformanceHistory = JsonSerializer.Deserialize<List<GamePerformance>>(perfJson) ?? new List<GamePerformance>();
        }

        // Load parent IDs if present
        if (data.TryGetProperty("ParentIds", out var parentIds))
        {
            individual.ParentIds = parentIds.EnumerateArray()
                .Select(x => x.GetGuid())
                .ToList();
        }

        return individual;
    }
}

/// <summary>
/// Represents performance data from a single game
/// </summary>
public class GamePerformance
{
    public DateTime GameStartTime { get; set; }
    public DateTime GameEndTime { get; set; }
    public TimeSpan GameDuration => GameEndTime - GameStartTime;
    
    // Core performance metrics
    public double Fitness { get; set; }
    public int Score { get; set; }
    public int PelletsCollected { get; set; }
    public int TimesCaptured { get; set; }
    public TimeSpan SurvivalTime { get; set; }
    public double AverageDistanceToGoal { get; set; }
    
    // Additional metrics
    public int MovesExecuted { get; set; }
    public int PowerUpsUsed { get; set; }
    public double DistanceTraveled { get; set; }
    public double EfficiencyRatio { get; set; } // Score per move
    
    // Game context
    public int TotalPlayers { get; set; }
    public int FinalRank { get; set; }
    public string GameMode { get; set; } = "Standard";
    public Dictionary<string, double> DetailedMetrics { get; set; }

    public GamePerformance()
    {
        DetailedMetrics = new Dictionary<string, double>();
        GameStartTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Calculates fitness based on multiple factors
    /// </summary>
    public void CalculateFitness()
    {
        // Multi-objective fitness function
        double scoreFactor = Score * 1.0;
        double survivalFactor = SurvivalTime.TotalSeconds * 0.1;
        double captureAvoidanceFactor = Math.Max(0, 100 - (TimesCaptured * 25));
        double efficiencyFactor = EfficiencyRatio * 10;
        double rankBonus = Math.Max(0, (TotalPlayers - FinalRank) * 5);
        
        Fitness = scoreFactor + survivalFactor + captureAvoidanceFactor + efficiencyFactor + rankBonus;
        
        // Store detailed metrics for analysis
        DetailedMetrics["ScoreFactor"] = scoreFactor;
        DetailedMetrics["SurvivalFactor"] = survivalFactor;
        DetailedMetrics["CaptureAvoidanceFactor"] = captureAvoidanceFactor;
        DetailedMetrics["EfficiencyFactor"] = efficiencyFactor;
        DetailedMetrics["RankBonus"] = rankBonus;
    }
} 