#pragma warning disable SKEXP0110

using StaticHeuro.GeneticAlgorithm;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace StaticHeuro;

/// <summary>
/// Simple coordinator that manages evolution and provides weights to bot instances
/// </summary>
public class EvolutionCoordinator
{
    private static readonly Lazy<EvolutionCoordinator> _instance = new(() => new EvolutionCoordinator());
    public static EvolutionCoordinator Instance => _instance.Value;

    private readonly EvolutionaryBotManager _evolutionManager;
    private readonly Dictionary<string, Guid> _runningBots = new();
    private readonly object _lock = new();
    private readonly Timer _evolutionTimer;
    private bool _isEvolutionRunning = false;
    private int _gamesPlayedSinceLastEvolution = 0;
    private const int GAMES_BEFORE_EVOLUTION = 5; // Evolve after every 5 games
    private const int EVOLUTION_INTERVAL_MINUTES = 10; // Also evolve every 10 minutes

    private EvolutionCoordinator()
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<EvolutionaryBotManager>();
        _evolutionManager = new EvolutionaryBotManager(logger);
        
        // Initialize evolution system synchronously to ensure it's ready
        try
        {
            Console.WriteLine("Initializing evolution system...");
            var initTask = _evolutionManager.InitializeAsync();
            initTask.Wait(TimeSpan.FromSeconds(10)); // Wait up to 10 seconds for initialization
            Console.WriteLine("Evolution system initialized successfully!");
            
            // Start the evolution process in background (now waits for performance data)
            _ = Task.Run(async () =>
            {
                try
                {
                    Console.WriteLine("Starting evolution process - will wait for game performance data...");
                    await StartEvolutionProcessAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in evolution process: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize evolution system: {ex.Message}");
            // Create a minimal population as fallback
            _evolutionManager.CurrentPopulation.InitializeRandom();
        }
        
        // Set up periodic evolution timer
        _evolutionTimer = new Timer(async _ => await TriggerEvolutionIfNeeded(), 
            null, 
            TimeSpan.FromMinutes(EVOLUTION_INTERVAL_MINUTES), 
            TimeSpan.FromMinutes(EVOLUTION_INTERVAL_MINUTES));
    }

    /// <summary>
    /// Starts the evolution process in the background - now waits for performance data
    /// </summary>
    private async Task StartEvolutionProcessAsync()
    {
        try
        {
            if (_isEvolutionRunning)
                return;
                
            _isEvolutionRunning = true;
            Console.WriteLine("Evolution process started - waiting for game performance data");
            
            // Start evolution with a cancellation token for graceful shutdown
            var cts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                try
                {
                    await _evolutionManager.StartEvolutionAsync(cts.Token);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Evolution process error: {ex.Message}");
                }
                finally
                {
                    _isEvolutionRunning = false;
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start evolution process: {ex.Message}");
            _isEvolutionRunning = false;
        }
    }

    /// <summary>
    /// Triggers evolution if enough games have been played
    /// </summary>
    private async Task TriggerEvolutionIfNeeded()
    {
        try
        {
            lock (_lock)
            {
                if (_gamesPlayedSinceLastEvolution < GAMES_BEFORE_EVOLUTION)
                    return;
                    
                _gamesPlayedSinceLastEvolution = 0;
            }
            
            Console.WriteLine("Triggering evolution due to game count threshold...");
            await EvolveAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in automatic evolution trigger: {ex.Message}");
        }
    }

    /// <summary>
    /// Registers a bot instance and gets assigned individual ID
    /// </summary>
    public Guid RegisterBot(string botNickname)
    {
        lock (_lock)
        {
            // If this nickname is already registered, return existing ID
            if (_runningBots.TryGetValue(botNickname, out var existingId))
            {
                Console.WriteLine($"Bot '{botNickname}' already registered with individual {existingId}");
                return existingId;
            }

            // Ensure population is initialized
            if (_evolutionManager.CurrentPopulation.Individuals.Count == 0)
            {
                Console.WriteLine("Population is empty, initializing random population...");
                _evolutionManager.CurrentPopulation.InitializeRandom();
            }

            // Get individuals that need evaluation (haven't played recently)
            var staleIndividuals = _evolutionManager.GetIndividualsNeedingEvaluation(TimeSpan.FromMinutes(30));
            Individual selectedIndividual;
            
            if (staleIndividuals.Any())
            {
                // Prioritize individuals that haven't been evaluated
                selectedIndividual = staleIndividuals[Random.Shared.Next(staleIndividuals.Count)];
                Console.WriteLine($"Bot '{botNickname}' assigned stale individual {selectedIndividual.Id} (Gen: {selectedIndividual.Generation}, Games: {selectedIndividual.GamesPlayed})");
            }
            else
            {
                // Get a random individual from current population
                var individuals = _evolutionManager.CurrentPopulation.Individuals;
                if (individuals.Any())
                {
                    selectedIndividual = individuals[Random.Shared.Next(individuals.Count)];
                    Console.WriteLine($"Bot '{botNickname}' assigned individual {selectedIndividual.Id} (Gen: {selectedIndividual.Generation}, Games: {selectedIndividual.GamesPlayed})");
                }
                else
                {
                    // Fallback: create a temporary individual and add it to population
                    selectedIndividual = new Individual();
                    _evolutionManager.CurrentPopulation.AddIndividual(selectedIndividual);
                    Console.WriteLine($"Bot '{botNickname}' assigned new individual {selectedIndividual.Id} (added to population)");
                }
            }
            
            _runningBots[botNickname] = selectedIndividual.Id;
            return selectedIndividual.Id;
        }
    }

    /// <summary>
    /// Records game performance for a bot
    /// </summary>
    public async Task RecordPerformanceAsync(string botNickname, int score, TimeSpan gameTime, int rank, int totalPlayers)
    {
        try
        {
            Guid botIndividualId;
            lock (_lock)
            {
                if (!_runningBots.TryGetValue(botNickname, out botIndividualId))
                {
                    Console.WriteLine($"No individual registered for bot '{botNickname}'");
                    return;
                }
                
                // Increment games played counter
                _gamesPlayedSinceLastEvolution++;
            }

            var performance = new GamePerformance
            {
                GameStartTime = DateTime.UtcNow - gameTime,
                GameEndTime = DateTime.UtcNow,
                Score = score,
                SurvivalTime = gameTime,
                FinalRank = rank,
                TotalPlayers = totalPlayers,
                TimesCaptured = Math.Max(0, totalPlayers - rank), // Estimate based on rank
                PelletsCollected = Math.Max(score, 0), // Assuming score correlates with pellets
                MovesExecuted = (int)(gameTime.TotalSeconds * 5), // Estimate ~5 moves per second
                DistanceTraveled = score * 1.5, // Rough estimate based on score
                EfficiencyRatio = gameTime.TotalSeconds > 0 ? score / gameTime.TotalSeconds : 0
            };

            await _evolutionManager.RecordGamePerformanceAsync(botIndividualId, performance);

            Console.WriteLine($"Performance recorded for '{botNickname}': Score={score}, Rank={rank}/{totalPlayers}, Time={gameTime.TotalSeconds:F1}s, Games since evolution: {_gamesPlayedSinceLastEvolution}");
            
            // Check if we should trigger evolution
            if (_gamesPlayedSinceLastEvolution >= GAMES_BEFORE_EVOLUTION)
            {
                _ = Task.Run(async () => await TriggerEvolutionIfNeeded());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error recording performance for '{botNickname}': {ex.Message}");
        }
    }

    /// <summary>
    /// Gets weights for a specific bot
    /// </summary>
    public async Task<string> GetWeightsJsonAsync(string botNickname)
    {
        try
        {
            lock (_lock)
            {
                if (!_runningBots.TryGetValue(botNickname, out var individualId))
                {
                    Console.WriteLine($"No individual registered for bot '{botNickname}', using default weights");
                    return "{}"; // Return empty JSON for default weights
                }
            }

            var individual = _evolutionManager.CurrentPopulation.Individuals
                .FirstOrDefault(i => i.Id == _runningBots[botNickname]);

            if (individual?.Genome != null)
            {
                var weightsJson = JsonSerializer.Serialize(individual.Genome, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine($"Providing evolved weights for '{botNickname}' from individual {individual.Id} (Gen: {individual.Generation}, Fitness: {individual.Fitness:F2})");
                return weightsJson;
            }

            Console.WriteLine($"No genome found for '{botNickname}', using default weights");
            return "{}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting weights for '{botNickname}': {ex.Message}");
            return "{}";
        }
    }

    /// <summary>
    /// Triggers evolution to next generation
    /// </summary>
    public async Task EvolveAsync()
    {
        try
        {
            Console.WriteLine("Starting manual evolution...");
            await _evolutionManager.EvolveOneGenerationAsync();
            Console.WriteLine($"Evolution completed! Now at generation {_evolutionManager.CurrentGeneration}");
            
            // Clear running bots so they get reassigned to new individuals
            lock (_lock)
            {
                _runningBots.Clear();
                _gamesPlayedSinceLastEvolution = 0;
            }
            
            // Print current statistics
            PrintStatistics();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during evolution: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the current best individual from the population
    /// </summary>
    public Individual? GetBestIndividual()
    {
        try
        {
            return _evolutionManager.GetBestIndividual();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting best individual: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets current evolution statistics
    /// </summary>
    public EvolutionStatistics GetStatistics()
    {
        try
        {
            return _evolutionManager.GetStatistics();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting statistics: {ex.Message}");
            return new EvolutionStatistics();
        }
    }

    /// <summary>
    /// Gets the current population
    /// </summary>
    public Population CurrentPopulation => _evolutionManager.CurrentPopulation;

    /// <summary>
    /// Exports the best individuals to a committable file
    /// </summary>
    public async Task ExportBestIndividualsAsync(int count = 5)
    {
        try
        {
            await _evolutionManager.ExportBestIndividualsAsync(count);
            Console.WriteLine($"Exported top {count} individuals to best-individuals.json");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting best individuals: {ex.Message}");
        }
    }

    /// <summary>
    /// Prints current evolution statistics to console
    /// </summary>
    public void PrintStatistics()
    {
        try
        {
            var stats = GetStatistics();
            var best = GetBestIndividual();

            Console.WriteLine("\n=== EVOLUTION STATISTICS ===");
            Console.WriteLine($"Generation: {stats.Generation}");
            Console.WriteLine($"Population Size: {stats.PopulationStatistics.PopulationSize}");
            Console.WriteLine($"Average Fitness: {stats.PopulationStatistics.AverageFitness:F2}");
            Console.WriteLine($"Best Fitness: {stats.PopulationStatistics.BestFitness:F2}");
            Console.WriteLine($"Population Diversity: {stats.PopulationStatistics.PopulationDiversity:F2}");
            Console.WriteLine($"Total Games Played: {stats.PopulationStatistics.TotalGamesPlayed}");
            Console.WriteLine($"Evolution Running: {_isEvolutionRunning}");
            Console.WriteLine($"Games Since Last Evolution: {_gamesPlayedSinceLastEvolution}");
            
            if (best != null)
            {
                Console.WriteLine($"Best Individual: Gen {best.Generation}, {best.GamesPlayed} games, Fitness {best.Fitness:F2}");
            }

            Console.WriteLine($"Running Bots: {string.Join(", ", _runningBots.Keys)}");
            Console.WriteLine("============================\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting statistics: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Cleanup resources
    /// </summary>
    public void Dispose()
    {
        _evolutionTimer?.Dispose();
        _evolutionManager?.StopEvolution();
    }
} 