#pragma warning disable SKEXP0110

using ClingyHeuroBot2.GeneticAlgorithm;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ClingyHeuroBot2;

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

    private EvolutionCoordinator()
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<EvolutionaryBotManager>();
        _evolutionManager = new EvolutionaryBotManager(logger);
        
        // Initialize evolution system
        _ = Task.Run(async () =>
        {
            try
            {
                await _evolutionManager.InitializeAsync();
                Console.WriteLine("Evolution system initialized successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize evolution system: {ex.Message}");
            }
        });
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
                return existingId;
            }

            // Get a random individual from current population for this bot
            var individuals = _evolutionManager.CurrentPopulation.Individuals;
            if (individuals.Any())
            {
                var randomIndividual = individuals[Random.Shared.Next(individuals.Count)];
                _runningBots[botNickname] = randomIndividual.Id;
                
                Console.WriteLine($"Bot '{botNickname}' assigned individual {randomIndividual.Id} (Gen: {randomIndividual.Generation})");
                return randomIndividual.Id;
            }

            // Fallback: create a temporary individual
            var tempIndividual = new Individual();
            _runningBots[botNickname] = tempIndividual.Id;
            Console.WriteLine($"Bot '{botNickname}' assigned temporary individual {tempIndividual.Id}");
            return tempIndividual.Id;
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
            }

            var performance = new GamePerformance
            {
                GameStartTime = DateTime.UtcNow - gameTime,
                GameEndTime = DateTime.UtcNow,
                Score = score,
                SurvivalTime = gameTime,
                FinalRank = rank,
                TotalPlayers = totalPlayers,
                TimesCaptured = 0, // Would need actual tracking
                PelletsCollected = score, // Assuming 1 point per pellet
                MovesExecuted = (int)gameTime.TotalSeconds, // Rough estimate
                DistanceTraveled = score * 2.0, // Rough estimate
                EfficiencyRatio = gameTime.TotalSeconds > 0 ? score / gameTime.TotalSeconds : 0
            };

            performance.CalculateFitness();

            await _evolutionManager.RecordGamePerformanceAsync(botIndividualId, performance);

            Console.WriteLine($"Recorded performance for '{botNickname}': Score={score}, Fitness={performance.Fitness:F2}");
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
                return JsonSerializer.Serialize(individual.Genome, new JsonSerializerOptions { WriteIndented = true });
            }

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
            await _evolutionManager.EvolveOneGenerationAsync();
            Console.WriteLine($"Evolution completed! Now at generation {_evolutionManager.CurrentGeneration}");
            
            // Clear running bots so they get reassigned to new individuals
            lock (_lock)
            {
                _runningBots.Clear();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during evolution: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets current evolution statistics
    /// </summary>
    public void PrintStatistics()
    {
        try
        {
            var stats = _evolutionManager.GetStatistics();
            var best = _evolutionManager.GetBestIndividual();

            Console.WriteLine("\n=== EVOLUTION STATISTICS ===");
            Console.WriteLine($"Generation: {stats.Generation}");
            Console.WriteLine($"Population Size: {stats.PopulationStatistics.PopulationSize}");
            Console.WriteLine($"Average Fitness: {stats.PopulationStatistics.AverageFitness:F2}");
            Console.WriteLine($"Best Fitness: {stats.PopulationStatistics.BestFitness:F2}");
            Console.WriteLine($"Population Diversity: {stats.PopulationStatistics.PopulationDiversity:F2}");
            
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
} 