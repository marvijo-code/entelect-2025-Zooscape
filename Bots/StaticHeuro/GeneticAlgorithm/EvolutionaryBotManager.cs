#pragma warning disable SKEXP0110

using Microsoft.Extensions.Logging;
using System.Text.Json;
using Marvijo.Zooscape.Bots.Common.Models;

namespace StaticHeuro.GeneticAlgorithm;

public interface IEvolutionaryBotManager {
    Task InitializeAsync();
    Task StartEvolutionAsync(CancellationToken cancellationToken);
    // Add other public methods as abstract
}

/// <summary>
/// Orchestrates the genetic algorithm evolution process for bot populations
/// </summary>
public class EvolutionaryBotManager : IEvolutionaryBotManager
{
    private readonly ILogger<EvolutionaryBotManager> _logger;
    private readonly GeneticAlgorithmConfig _config;
    private readonly FitnessEvaluator _fitnessEvaluator;
    private readonly Random _random;
    private readonly string _dataDirectory;
    private readonly HighScoreTracker _highScoreTracker;

    public Population CurrentPopulation { get; private set; }
    public int CurrentGeneration => CurrentPopulation?.Generation ?? 0;
    public bool IsRunning { get; private set; }
    public DateTime? LastEvolutionTime { get; private set; }
    
    public event EventHandler<EvolutionEventArgs>? GenerationCompleted;
    public event EventHandler<EvolutionEventArgs>? NewBestIndividual;
    public event EventHandler<EvolutionEventArgs>? EvolutionStopped;

    public EvolutionaryBotManager(
        ILogger<EvolutionaryBotManager> logger,
        GeneticAlgorithmConfig? config = null,
        FitnessConfig? fitnessConfig = null,
        string? dataDirectory = null)
    {
        _logger = logger;
        _config = config ?? new GeneticAlgorithmConfig();
        _fitnessEvaluator = new FitnessEvaluator(fitnessConfig);
        _random = new Random();
        _dataDirectory = dataDirectory ?? Path.Combine(Environment.CurrentDirectory, "evolution-data");
        
        Directory.CreateDirectory(_dataDirectory);
        
        CurrentPopulation = new Population(_config.PopulationSize);
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _highScoreTracker = new HighScoreTracker(loggerFactory.CreateLogger<HighScoreTracker>(), _dataDirectory);
    }

    /// <summary>
    /// Initializes the evolutionary process with a new population
    /// </summary>
    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing evolutionary bot manager...");
        
        // Try to load existing population first
        var populationDir = Path.Combine(_dataDirectory, "current-population");
        if (Directory.Exists(populationDir))
        {
            try
            {
                CurrentPopulation = Population.LoadFromDirectory(populationDir);
                _logger.LogInformation($"Loaded existing population: Generation {CurrentPopulation.Generation}, Size {CurrentPopulation.Individuals.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load existing population, creating new one");
                CurrentPopulation.InitializeRandom();
            }
        }
        else
        {
            CurrentPopulation.InitializeRandom();
            _logger.LogInformation($"Created new random population with {CurrentPopulation.Individuals.Count} individuals");
        }

        // Always attempt to import best individuals
        await ImportBestIndividualsAsync();

        // If population exceeds max size after import, remove worst individuals
        if (CurrentPopulation.Individuals.Count > _config.PopulationSize)
        {
            var excess = CurrentPopulation.Individuals.Count - _config.PopulationSize;
            CurrentPopulation.RemoveWorstIndividuals(excess);
            _logger.LogInformation($"Trimmed population by removing {excess} worst individuals after import. New size: {CurrentPopulation.Individuals.Count}");
        }

        // Save initial population
        await SaveCurrentPopulationAsync();
        
        _logger.LogInformation("Evolutionary bot manager initialized successfully");
    }

    /// <summary>
    /// Checks if the population has sufficient performance data to evolve
    /// </summary>
    private bool HasSufficientPerformanceData()
    {
        // Require at least 50% of population to have non-zero fitness
        var individualsWithFitness = CurrentPopulation.Individuals.Count(i => i.Fitness > 0);
        var minRequired = Math.Max(1, CurrentPopulation.Individuals.Count / 2);
        
        return individualsWithFitness >= minRequired;
    }

    /// <summary>
    /// Starts the evolution process (now waits for sufficient performance data)
    /// </summary>
    public async Task StartEvolutionAsync(CancellationToken cancellationToken = default)
    {
        IsRunning = true;
        _logger.LogInformation("Starting evolution process - waiting for game performance data...");

        try
        {
            int stagnationCount = 0;
            double lastBestFitness = 0;
            int waitCycles = 0;

            while (IsRunning && CurrentGeneration < _config.MaxGenerations && !cancellationToken.IsCancellationRequested)
            {
                // Check if we have sufficient performance data before evolving
                if (!HasSufficientPerformanceData())
                {
                    waitCycles++;
                    if (waitCycles % 100 == 0) // Log every 10 seconds
                    {
                        var individualsWithFitness = CurrentPopulation.Individuals.Count(i => i.Fitness > 0);
                        var minRequired = Math.Max(1, CurrentPopulation.Individuals.Count / 2);
                        _logger.LogInformation($"Waiting for performance data: {individualsWithFitness}/{CurrentPopulation.Individuals.Count} individuals have fitness data (need {minRequired})");
                    }
                    
                    await Task.Delay(100, cancellationToken); // Wait for games to complete
                    continue;
                }

                // Reset wait counter once we have data
                if (waitCycles > 0)
                {
                    _logger.LogInformation("Sufficient performance data available, starting evolution...");
                    waitCycles = 0;
                }

                await EvolveOneGenerationAsync();
                
                var currentBestFitness = CurrentPopulation.BestFitness;
                var improvement = currentBestFitness - lastBestFitness;

                // Check for stagnation
                if (improvement < _config.ConvergenceThreshold)
                {
                    stagnationCount++;
                    if (stagnationCount >= _config.StagnationLimit)
                    {
                        _logger.LogInformation($"Evolution stopped due to stagnation after {stagnationCount} generations without improvement");
                        break;
                    }
                }
                else
                {
                    stagnationCount = 0;
                    NotifyNewBestIndividual();
                    
                    // Auto-export best individuals when significant improvement is made
                    if (improvement > 0.1) // Export on meaningful improvements
                    {
                        _ = Task.Run(async () => await ExportBestIndividualsAsync());
                    }
                }

                lastBestFitness = currentBestFitness;
                LastEvolutionTime = DateTime.UtcNow;

                // Longer delay between generations to allow for more games
                await Task.Delay(5000, cancellationToken); // 5 second delay
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Evolution process was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during evolution process");
        }
        finally
        {
            IsRunning = false;
            EvolutionStopped?.Invoke(this, new EvolutionEventArgs(CurrentPopulation, CurrentGeneration));
        }
    }

    /// <summary>
    /// Stops the evolution process
    /// </summary>
    public void StopEvolution()
    {
        IsRunning = false;
        _logger.LogInformation("Evolution process stop requested");
    }

    /// <summary>
    /// Evolves the population by one generation
    /// </summary>
    public async Task EvolveOneGenerationAsync()
    {
        _logger.LogDebug($"Evolving generation {CurrentGeneration}");

        var newIndividuals = new List<Individual>();
        
        // Preserve elite individuals
        var elites = CurrentPopulation.GetTopIndividuals(_config.EliteCount);
        newIndividuals.AddRange(elites.Select(e => e.Clone()));

        // Generate new individuals through crossover and mutation
        while (newIndividuals.Count < _config.PopulationSize) {
            var tasks = new List<Task<Individual>>();
            for (int i = 0; i < Math.Min(2, _config.PopulationSize - newIndividuals.Count); i++) {
                tasks.Add(Task.Run(() => {
                    var parent = SelectParent();
                    return ApplyMutation(parent.Clone());
                }));
            }
            var children = await Task.WhenAll(tasks);
            newIndividuals.AddRange(children);
        }

        // Replace population
        CurrentPopulation.Individuals.Clear();
        CurrentPopulation.Individuals.AddRange(newIndividuals);
        CurrentPopulation.NextGeneration();

        // Save population and notify
        await SaveCurrentPopulationAsync();
        GenerationCompleted?.Invoke(this, new EvolutionEventArgs(CurrentPopulation, CurrentGeneration));

        _logger.LogInformation($"Generation {CurrentGeneration} completed. Population size: {CurrentPopulation.Individuals.Count}, Best fitness: {CurrentPopulation.BestFitness:F2}");
    }

    /// <summary>
    /// Records game performance for an individual
    /// </summary>
    public async Task RecordGamePerformanceAsync(Guid individualId, GamePerformance performance)
    {
        var individual = CurrentPopulation.Individuals.FirstOrDefault(i => i.Id == individualId);
        if (individual == null)
        {
            _logger.LogWarning($"Individual {individualId} not found in current population");
            return;
        }

        // Calculate fitness for the performance
        performance.Fitness = _fitnessEvaluator.CalculateFitness(performance);
        individual.RecordPerformance(performance);

        _logger.LogDebug($"Recorded performance for individual {individualId}: Score={performance.Score}, Fitness={performance.Fitness:F2}");

        // Update high scores if necessary
        await _highScoreTracker.RecordPerformanceAsync(individual, performance, CurrentGeneration);
    }

    /// <summary>
    /// Gets the current best individual
    /// </summary>
    public Individual? GetBestIndividual()
    {
        return CurrentPopulation.BestIndividual;
    }

    /// <summary>
    /// Gets individuals that need evaluation (haven't played recently)
    /// </summary>
    public List<Individual> GetIndividualsNeedingEvaluation(TimeSpan maxAge)
    {
        return CurrentPopulation.GetStaleIndividuals(maxAge);
    }

    /// <summary>
    /// Selects a parent using the configured selection method
    /// </summary>
    private Individual SelectParent()
    {
        return _config.SelectionMethod.ToLower() switch
        {
            "tournament" => GeneticOperators.TournamentSelection(CurrentPopulation, _config.TournamentSize, _random),
            "roulette" => GeneticOperators.RouletteWheelSelection(CurrentPopulation, _random),
            "rank" => GeneticOperators.RankSelection(CurrentPopulation, _random),
            _ => GeneticOperators.TournamentSelection(CurrentPopulation, _config.TournamentSize, _random)
        };
    }

    /// <summary>
    /// Applies crossover operation between two parents
    /// </summary>
    private (Individual child1, Individual child2) ApplyCrossover(Individual parent1, Individual parent2)
    {
        return _config.CrossoverMethod.ToLower() switch
        {
            "uniform" => GeneticOperators.UniformCrossover(parent1, parent2, _random),
            "blend" => GeneticOperators.BlendCrossover(parent1, parent2, _config.BlendAlpha, _random),
            "arithmetic" => GeneticOperators.ArithmeticCrossover(parent1, parent2, 0.5, _random),
            _ => GeneticOperators.BlendCrossover(parent1, parent2, _config.BlendAlpha, _random)
        };
    }

    /// <summary>
    /// Applies mutation operation to an individual
    /// </summary>
    private Individual ApplyMutation(Individual individual)
    {
        var effectiveRate = _config.MutationRate;
        if (CurrentPopulation.PopulationDiversity < 0.3) effectiveRate *= 1.5;
        if (_random.NextDouble() >= effectiveRate) return individual; // No mutation

        return _config.MutationMethod.ToLower() switch
        {
            "gaussian" => GeneticOperators.GaussianMutation(individual, _config.MutationRate, _config.MutationSigma, _random),
            "uniform" => GeneticOperators.UniformMutation(individual, _config.MutationRate, _random),
            "adaptive" => GeneticOperators.AdaptiveMutation(individual, _config.MutationRate, CurrentPopulation.PopulationDiversity, _random),
            _ => GeneticOperators.GaussianMutation(individual, _config.MutationRate, _config.MutationSigma, _random)
        };
    }

    /// <summary>
    /// Saves the current population to disk
    /// </summary>
    private async Task SaveCurrentPopulationAsync()
    {
        try
        {
            var populationDir = Path.Combine(_dataDirectory, "current-population");
            await Task.Run(() => CurrentPopulation.SaveToDirectory(populationDir));
            
            // Also save a backup with generation number
            var backupDir = Path.Combine(_dataDirectory, "backups", $"generation-{CurrentGeneration:D4}");
            await Task.Run(() => CurrentPopulation.SaveToDirectory(backupDir));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save current population");
        }
    }

    /// <summary>
    /// Updates high scores file with new best performances
    /// </summary>
    private async Task UpdateHighScoresAsync(Individual individual, GamePerformance performance)
    {
        try
        {
            await _highScoreTracker.RecordPerformanceAsync(individual, performance, CurrentGeneration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update high scores");
        }
    }

    /// <summary>
    /// Notifies about a new best individual
    /// </summary>
    private void NotifyNewBestIndividual()
    {
        var bestIndividual = GetBestIndividual();
        if (bestIndividual != null)
        {
            _logger.LogInformation($"New best individual found: {bestIndividual.GetPerformanceSummary()}");
            NewBestIndividual?.Invoke(this, new EvolutionEventArgs(CurrentPopulation, CurrentGeneration, bestIndividual));
        }
    }

    /// <summary>
    /// Gets evolution statistics
    /// </summary>
    public EvolutionStatistics GetStatistics()
    {
        var populationStats = CurrentPopulation.GetStatistics();
        
        return new EvolutionStatistics
        {
            Generation = CurrentGeneration,
            PopulationStatistics = populationStats,
            IsRunning = IsRunning,
            LastEvolutionTime = LastEvolutionTime,
            BestIndividual = GetBestIndividual(),
            ConfigurationSummary = new Dictionary<string, object>
            {
                ["PopulationSize"] = _config.PopulationSize,
                ["SelectionMethod"] = _config.SelectionMethod,
                ["CrossoverMethod"] = _config.CrossoverMethod,
                ["MutationMethod"] = _config.MutationMethod,
                ["CrossoverRate"] = _config.CrossoverRate,
                ["MutationRate"] = _config.MutationRate
            }
        };
    }

    /// <summary>
    /// Exports the best individuals to a committable file
    /// </summary>
    public async Task ExportBestIndividualsAsync(int topCount = 5)
    {
        try
        {
            var bestIndividuals = CurrentPopulation.GetTopIndividuals(topCount);
            var exportData = new
            {
                ExportedAt = DateTime.UtcNow,
                Generation = CurrentGeneration,
                TotalGenerations = CurrentGeneration,
                PopulationSize = CurrentPopulation.Individuals.Count,
                BestFitness = CurrentPopulation.BestFitness,
                Individuals = bestIndividuals.Select(ind => new
                {
                    Id = ind.Id,
                    Generation = ind.Generation,
                    Fitness = ind.Fitness,
                    GamesPlayed = ind.GamesPlayed,
                    Age = ind.Age,
                    EvolutionMethod = ind.EvolutionMethod,
                    PerformanceSummary = ind.GetPerformanceSummary(),
                    Genome = ind.Genome,
                    BestPerformance = ind.PerformanceHistory.OrderByDescending(p => p.Fitness).FirstOrDefault()
                }).ToList()
            };

            var exportPath = "best-individuals.json";
            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(exportPath, json);

            _logger.LogInformation($"Exported top {bestIndividuals.Count} individuals to {exportPath}");
            
            // Also create a backup with timestamp
            var timestampedPath = $"best-individuals-gen{CurrentGeneration:D4}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            await File.WriteAllTextAsync(timestampedPath, json);
            _logger.LogInformation($"Created timestamped backup: {timestampedPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export best individuals");
        }
    }

    /// <summary>
    /// Imports best individuals from a file to seed the population
    /// </summary>
    public async Task ImportBestIndividualsAsync(string filePath = "best-individuals.json")
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogInformation($"No import file found at {filePath}");
                return;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var importData = JsonSerializer.Deserialize<JsonElement>(json);
            
            if (!importData.TryGetProperty("Individuals", out var individualsArray))
            {
                _logger.LogWarning("Invalid import file format - missing Individuals array");
                return;
            }

            var importedCount = 0;
            foreach (var indElement in individualsArray.EnumerateArray())
            {
                try
                {
                    if (indElement.TryGetProperty("Genome", out var genomeElement))
                    {
                        var genome = JsonSerializer.Deserialize<HeuristicWeights>(genomeElement.GetRawText());
                        if (genome != null)
                        {
                            var individual = new Individual(genome)
                            {
                                EvolutionMethod = "Imported"
                            };
                            
                            // Try to restore fitness if available
                            if (indElement.TryGetProperty("Fitness", out var fitnessElement))
                            {
                                individual.Fitness = fitnessElement.GetDouble();
                            }

                            CurrentPopulation.Individuals.Add(individual);
                            importedCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to import individual");
                }
            }

            _logger.LogInformation($"Imported {importedCount} best individuals from {filePath}");
            
            // If we imported individuals, trim population to max size
            if (importedCount > 0 && CurrentPopulation.Individuals.Count > _config.PopulationSize)
            {
                var excess = CurrentPopulation.Individuals.Count - _config.PopulationSize;
                CurrentPopulation.Individuals.RemoveRange(_config.PopulationSize, excess);
                _logger.LogInformation($"Trimmed population to {_config.PopulationSize} individuals");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to import best individuals from {filePath}");
        }
    }

    private void AssignParetoRanks() {
        // Simple NSGA-II inspired ranking
        var fronts = new List<List<Individual>>();
        // Implementation details omitted for brevity; assign ranks based on dominance
    }
}

/// <summary>
/// Event arguments for evolution events
/// </summary>
public class EvolutionEventArgs : EventArgs
{
    public Population Population { get; }
    public int Generation { get; }
    public Individual? BestIndividual { get; }
    public DateTime Timestamp { get; }

    public EvolutionEventArgs(Population population, int generation, Individual? bestIndividual = null)
    {
        Population = population;
        Generation = generation;
        BestIndividual = bestIndividual;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Complete evolution statistics
/// </summary>
public class EvolutionStatistics
{
    public int Generation { get; set; }
    public PopulationStatistics PopulationStatistics { get; set; } = new();
    public bool IsRunning { get; set; }
    public DateTime? LastEvolutionTime { get; set; }
    public Individual? BestIndividual { get; set; }
    public Dictionary<string, object> ConfigurationSummary { get; set; } = new();

    public override string ToString()
    {
        return $"Evolution Statistics - Gen: {Generation}, Running: {IsRunning}\n" +
               $"Population: {PopulationStatistics}\n" +
               $"Best Individual: {BestIndividual?.GetPerformanceSummary() ?? "None"}";
    }
} 