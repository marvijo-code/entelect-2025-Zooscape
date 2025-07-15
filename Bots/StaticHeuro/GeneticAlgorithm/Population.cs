#pragma warning disable SKEXP0110

using Marvijo.Zooscape.Bots.Common.Models;
using System.Text.Json;

namespace StaticHeuro.GeneticAlgorithm;

/// <summary>
/// Manages a population of bot individuals for genetic algorithm evolution
/// </summary>
public class Population
{
    public List<Individual> Individuals { get; private set; }
    public int MaxSize { get; set; }
    public int Generation { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastEvolvedAt { get; set; }
    public Random Random { get; private set; }

    // Population statistics
    public Individual BestIndividual => Individuals.OrderByDescending(i => i.Fitness).FirstOrDefault();
    public Individual WorstIndividual => Individuals.OrderBy(i => i.Fitness).FirstOrDefault();
    public double AverageFitness => Individuals.Count > 0 ? Individuals.Average(i => i.Fitness) : 0;
    public double BestFitness => Individuals.Count > 0 ? Individuals.Max(i => i.Fitness) : 0;
    public double WorstFitness => Individuals.Count > 0 ? Individuals.Min(i => i.Fitness) : 0;
    public double FitnessStandardDeviation => CalculateFitnessStandardDeviation();
    public double PopulationDiversity => CalculatePopulationDiversity();

    public Population(int maxSize, int? seed = null)
    {
        MaxSize = maxSize;
        Individuals = new List<Individual>();
        Generation = 0;
        CreatedAt = DateTime.UtcNow;
        Random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Initializes the population with random individuals
    /// </summary>
    public void InitializeRandom()
    {
        Individuals.Clear();
        
        for (int i = 0; i < MaxSize; i++)
        {
            var individual = CreateRandomIndividual();
            individual.Generation = 0;
            Individuals.Add(individual);
        }

        Generation = 0;
        LastEvolvedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a random individual with randomized heuristic weights
    /// </summary>
    private Individual CreateRandomIndividual()
    {
        var weights = new HeuristicWeights();
        var properties = typeof(HeuristicWeights).GetProperties()
            .Where(p => p.CanWrite && (p.PropertyType == typeof(decimal) || p.PropertyType == typeof(int)));

        foreach (var property in properties)
        {
            if (property.PropertyType == typeof(decimal))
            {
                // Generate random decimal values in reasonable ranges
                decimal value = GetRandomDecimalForProperty(property.Name);
                property.SetValue(weights, value);
            }
            else if (property.PropertyType == typeof(int))
            {
                // Generate random int values in reasonable ranges
                int value = GetRandomIntForProperty(property.Name);
                property.SetValue(weights, value);
            }
        }

        return new Individual(weights);
    }

    /// <summary>
    /// Gets a random decimal value for a specific property based on typical ranges
    /// </summary>
    private decimal GetRandomDecimalForProperty(string propertyName)
    {
        // Define reasonable ranges for different types of properties
        return propertyName.ToLower() switch
        {
            var name when name.Contains("penalty") => (decimal)(Random.NextDouble() * -50.0 - 5.0), // -55 to -5
            var name when name.Contains("bonus") => (decimal)(Random.NextDouble() * 50.0 + 5.0), // 5 to 55
            var name when name.Contains("weight") || name.Contains("factor") => (decimal)(Random.NextDouble() * 20.0 - 10.0), // -10 to 10
            var name when name.Contains("distance") => (decimal)(Random.NextDouble() * 100.0), // 0 to 100
            var name when name.Contains("ratio") || name.Contains("percent") => (decimal)(Random.NextDouble() * 2.0), // 0 to 2
            var name when name.Contains("divisor") => (decimal)(Random.NextDouble() * 10.0 + 1.0), // 1 to 11
            _ => (decimal)(Random.NextDouble() * 20.0 - 10.0) // Default: -10 to 10
        };
    }

    /// <summary>
    /// Gets a random int value for a specific property based on typical ranges
    /// </summary>
    private int GetRandomIntForProperty(string propertyName)
    {
        return propertyName.ToLower() switch
        {
            var name when name.Contains("threshold") => Random.Next(1, 100),
            var name when name.Contains("size") || name.Contains("window") => Random.Next(3, 20),
            var name when name.Contains("interval") => Random.Next(1, 50),
            var name when name.Contains("distance") => Random.Next(1, 20),
            _ => Random.Next(1, 100)
        };
    }

    /// <summary>
    /// Adds an individual to the population
    /// </summary>
    public void AddIndividual(Individual individual)
    {
        if (Individuals.Count < MaxSize)
        {
            Individuals.Add(individual);
        }
        else
        {
            // Replace worst individual if new one is better
            var worstIndividual = WorstIndividual;
            if (individual.Fitness > worstIndividual.Fitness)
            {
                Individuals.Remove(worstIndividual);
                Individuals.Add(individual);
            }
        }
    }

    /// <summary>
    /// Removes an individual from the population
    /// </summary>
    public bool RemoveIndividual(Individual individual)
    {
        return Individuals.Remove(individual);
    }

    /// <summary>
    /// Removes the worst individuals to make room for new ones
    /// </summary>
    public void RemoveWorstIndividuals(int count)
    {
        var sortedIndividuals = Individuals.OrderBy(i => i.Fitness).ToList();
        for (int i = 0; i < Math.Min(count, sortedIndividuals.Count); i++)
        {
            Individuals.Remove(sortedIndividuals[i]);
        }
    }

    /// <summary>
    /// Gets the top N individuals by fitness
    /// </summary>
    public List<Individual> GetTopIndividuals(int count)
    {
        return Individuals.OrderByDescending(i => i.Fitness)
                         .Take(count)
                         .ToList();
    }

    /// <summary>
    /// Gets individuals that haven't been evaluated recently
    /// </summary>
    public List<Individual> GetStaleIndividuals(TimeSpan maxAge)
    {
        var cutoffTime = DateTime.UtcNow - maxAge;
        return Individuals.Where(i => i.LastEvaluatedAt < cutoffTime).ToList();
    }

    /// <summary>
    /// Calculates the standard deviation of fitness values
    /// </summary>
    private double CalculateFitnessStandardDeviation()
    {
        if (Individuals.Count <= 1) return 0;

        var mean = AverageFitness;
        var sumSquaredDeviations = Individuals.Sum(i => Math.Pow(i.Fitness - mean, 2));
        return Math.Sqrt(sumSquaredDeviations / (Individuals.Count - 1));
    }

    /// <summary>
    /// Calculates population diversity based on genome differences
    /// </summary>
    private double CalculatePopulationDiversity()
    {
        if (Individuals.Count <= 1) return 0;

        double totalDiversity = 0;
        int comparisons = 0;

        for (int i = 0; i < Individuals.Count; i++)
        {
            for (int j = i + 1; j < Individuals.Count; j++)
            {
                totalDiversity += CalculateGenomicDistance(Individuals[i], Individuals[j]);
                comparisons++;
            }
        }

        return comparisons > 0 ? totalDiversity / comparisons : 0;
    }

    /// <summary>
    /// Calculates the genomic distance between two individuals
    /// </summary>
    private double CalculateGenomicDistance(Individual individual1, Individual individual2)
    {
        var properties = typeof(HeuristicWeights).GetProperties()
            .Where(p => p.PropertyType == typeof(decimal) || p.PropertyType == typeof(int));

        double sumSquaredDifferences = 0;
        int propertyCount = 0;

        foreach (var property in properties)
        {
            var value1 = Convert.ToDouble(property.GetValue(individual1.Genome));
            var value2 = Convert.ToDouble(property.GetValue(individual2.Genome));
            sumSquaredDifferences += Math.Pow(value1 - value2, 2);
            propertyCount++;
        }

        return propertyCount > 0 ? Math.Sqrt(sumSquaredDifferences / propertyCount) : 0;
    }

    /// <summary>
    /// Advances to the next generation
    /// </summary>
    public void NextGeneration()
    {
        Generation++;
        LastEvolvedAt = DateTime.UtcNow;
        
        // Age all individuals
        foreach (var individual in Individuals)
        {
            individual.Age++;
        }
    }

    /// <summary>
    /// Gets population statistics summary
    /// </summary>
    public PopulationStatistics GetStatistics()
    {
        return new PopulationStatistics
        {
            Generation = Generation,
            PopulationSize = Individuals.Count,
            MaxSize = MaxSize,
            AverageFitness = AverageFitness,
            BestFitness = BestFitness,
            WorstFitness = WorstFitness,
            FitnessStandardDeviation = FitnessStandardDeviation,
            PopulationDiversity = PopulationDiversity,
            AverageAge = Individuals.Count > 0 ? Individuals.Average(i => i.Age) : 0,
            OldestAge = Individuals.Count > 0 ? Individuals.Max(i => i.Age) : 0,
            TotalGamesPlayed = Individuals.Sum(i => i.GamesPlayed),
            LastEvolvedAt = LastEvolvedAt
        };
    }

    /// <summary>
    /// Saves the population to a directory
    /// </summary>
    public void SaveToDirectory(string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);

        // Save population metadata
        var metadata = new
        {
            Generation,
            MaxSize,
            CreatedAt,
            LastEvolvedAt,
            Statistics = GetStatistics()
        };

        var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(directoryPath, "population-metadata.json"), metadataJson);

        // Save each individual
        for (int i = 0; i < Individuals.Count; i++)
        {
            var individual = Individuals[i];
            var filename = $"individual-{i:D3}-{individual.Id:N}.json";
            individual.SaveToFile(Path.Combine(directoryPath, filename));
        }
    }

    /// <summary>
    /// Loads a population from a directory
    /// </summary>
    public static Population LoadFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Population directory not found: {directoryPath}");

        // Load metadata
        var metadataPath = Path.Combine(directoryPath, "population-metadata.json");
        if (!File.Exists(metadataPath))
            throw new FileNotFoundException($"Population metadata file not found: {metadataPath}");

        var metadataJson = File.ReadAllText(metadataPath);
        var metadata = JsonSerializer.Deserialize<JsonElement>(metadataJson);

        var population = new Population(metadata.GetProperty("MaxSize").GetInt32())
        {
            Generation = metadata.GetProperty("Generation").GetInt32(),
            CreatedAt = metadata.GetProperty("CreatedAt").GetDateTime(),
            LastEvolvedAt = metadata.GetProperty("LastEvolvedAt").GetDateTime()
        };

        // Load individuals
        var individualFiles = Directory.GetFiles(directoryPath, "individual-*.json");
        foreach (var file in individualFiles)
        {
            try
            {
                var individual = Individual.LoadFromFile(file);
                population.Individuals.Add(individual);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to load individual from {file}: {ex.Message}");
            }
        }

        return population;
    }
}

/// <summary>
/// Contains statistical information about a population
/// </summary>
public class PopulationStatistics
{
    public int Generation { get; set; }
    public int PopulationSize { get; set; }
    public int MaxSize { get; set; }
    public double AverageFitness { get; set; }
    public double BestFitness { get; set; }
    public double WorstFitness { get; set; }
    public double FitnessStandardDeviation { get; set; }
    public double PopulationDiversity { get; set; }
    public double AverageAge { get; set; }
    public double OldestAge { get; set; }
    public int TotalGamesPlayed { get; set; }
    public DateTime LastEvolvedAt { get; set; }

    public override string ToString()
    {
        return $"Gen: {Generation}, Size: {PopulationSize}/{MaxSize}, " +
               $"Fitness: Avg={AverageFitness:F2} Best={BestFitness:F2} Worst={WorstFitness:F2} " +
               $"StdDev={FitnessStandardDeviation:F2}, Diversity={PopulationDiversity:F2}, " +
               $"Age: Avg={AverageAge:F1} Max={OldestAge}, Games={TotalGamesPlayed}";
    }
} 