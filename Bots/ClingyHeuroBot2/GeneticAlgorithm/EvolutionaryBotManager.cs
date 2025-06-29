#pragma warning disable SKEXP0110

using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ClingyHeuroBot2.GeneticAlgorithm;

/// <summary>
/// Orchestrates the genetic algorithm evolution process for bot populations
/// </summary>
public class EvolutionaryBotManager
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

        // Save initial population
        await SaveCurrentPopulationAsync();
        
        _logger.LogInformation("Evolutionary bot manager initialized successfully");
    }

    /// <summary>
    /// Starts the evolution process
    /// </summary>
    public async Task StartEvolutionAsync(CancellationToken cancellationToken = default)
    {
        IsRunning = true;
        _logger.LogInformation("Starting evolution process...");

        try
        {
            int stagnationCount = 0;
            double lastBestFitness = 0;

            while (IsRunning && CurrentGeneration < _config.MaxGenerations && !cancellationToken.IsCancellationRequested)
            {
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
                }

                lastBestFitness = currentBestFitness;
                LastEvolutionTime = DateTime.UtcNow;

                // Small delay between generations
                await Task.Delay(100, cancellationToken);
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
        while (newIndividuals.Count < _config.PopulationSize)
        {
            if (newIndividuals.Count + 1 >= _config.PopulationSize)
            {
                // Create single individual through mutation
                var parent = SelectParent();
                var mutatedChild = ApplyMutation(parent);
                newIndividuals.Add(mutatedChild);
            }
            else
            {
                // Create two children through crossover
                var parent1 = SelectParent();
                var parent2 = SelectParent();

                if (_random.NextDouble() < _config.CrossoverRate)
                {
                    var (child1, child2) = ApplyCrossover(parent1, parent2);
                    
                    child1 = ApplyMutation(child1);
                    child2 = ApplyMutation(child2);
                    
                    newIndividuals.Add(child1);
                    if (newIndividuals.Count < _config.PopulationSize)
                        newIndividuals.Add(child2);
                }
                else
                {
                    // No crossover, just mutate parents
                    newIndividuals.Add(ApplyMutation(parent1.Clone()));
                    if (newIndividuals.Count < _config.PopulationSize)
                        newIndividuals.Add(ApplyMutation(parent2.Clone()));
                }
            }
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
        if (_random.NextDouble() >= _config.MutationRate)
            return individual; // No mutation

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