using Marvijo.Zooscape.Bots.Common.Models;
using System.IO;
using System.Text.Json;

namespace ClingyHeuroBot2.Heuristics;

public static class WeightManager
{
    private static HeuristicWeights? _staticWeights;
    private static readonly object _lock = new();
    private static DateTime _lastWeightsUpdate = DateTime.MinValue;
    private static readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(30); // Update weights every 30 seconds

    // Cache to avoid querying coordinator on every tick (and spamming logs)
    private static HeuristicWeights? _cachedWeights;
    private static Guid? _cachedIndividualId;

    static WeightManager()
    {
        LoadStaticWeights();
    }

    /// <summary>
    /// Gets the current best weights from evolution or falls back to static weights
    /// </summary>
    public static HeuristicWeights Instance 
    {
        get
        {
            try
            {
                // Ensure static weights are loaded first (thread-safe)
                lock (_lock)
                {
                    if (_staticWeights == null)
                    {
                        LoadStaticWeights();
                    }
                }

                // Always ensure we have valid fallback weights first
                var fallbackWeights = _staticWeights ?? new HeuristicWeights();

                // Fast-path: return cached weights if still fresh
                if (_cachedWeights != null && (DateTime.UtcNow - _lastWeightsUpdate) < _updateInterval)
                {
                    return _cachedWeights;
                }

                // Check if we need to update weights (cache expired)
                {
                    lock (_lock)
                    {
                        if (DateTime.UtcNow - _lastWeightsUpdate > _updateInterval)
                        {
                            _lastWeightsUpdate = DateTime.UtcNow;
                            var updatedWeights = GetEvolvedWeights();
                            if (updatedWeights != null)
                            {
                                // Additional validation to ensure weights are not corrupted
                                if (ValidateWeights(updatedWeights))
                                {
                                    return updatedWeights;
                                }
                                else
                                {
                                    Console.WriteLine("WARNING: Evolved weights failed validation, using fallback");
                                }
                            }
                        }
                    }
                }

                // Try to get evolved weights first
                var evolvedWeights = GetEvolvedWeights();
                if (evolvedWeights != null && ValidateWeights(evolvedWeights))
                {
                    return evolvedWeights;
                }

                // Fallback to static weights if evolution system isn't available
                Console.WriteLine("Using fallback weights - evolution system not ready yet");
                return fallbackWeights;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting evolved weights, using fallback: {ex.Message}");
                // Ensure we NEVER return null - create a new instance if needed
                var fallback = _staticWeights ?? new HeuristicWeights();
                if (fallback == null) // Extra paranoid check
                {
                    Console.WriteLine("CRITICAL: Creating emergency default weights!");
                    fallback = new HeuristicWeights();
                }
                
                // Final validation to ensure we never return null
                return fallback ?? new HeuristicWeights();
            }
        }
    }

    /// <summary>
    /// Gets evolved weights from the evolution coordinator
    /// </summary>
    private static HeuristicWeights? GetEvolvedWeights()
    {
        try
        {
            // Respect cache – only refresh if interval has passed
            if (_cachedWeights != null && (DateTime.UtcNow - _lastWeightsUpdate) < _updateInterval)
            {
                return _cachedWeights;
            }

            var coordinator = ClingyHeuroBot2.EvolutionCoordinator.Instance;
            var bestIndividual = coordinator.GetBestIndividual();

            if (bestIndividual?.Genome != null)
            {
                // Only log when individual changes to reduce spam
                if (_cachedIndividualId != bestIndividual.Id)
                {
                    Console.WriteLine($"Using evolved weights from individual {bestIndividual.Id} (Gen: {bestIndividual.Generation}, Fitness: {bestIndividual.Fitness:F2}, Games: {bestIndividual.GamesPlayed})");
                }
                _cachedIndividualId = bestIndividual.Id;
                _cachedWeights = bestIndividual.Genome;
                _lastWeightsUpdate = DateTime.UtcNow;
                return _cachedWeights;
            }

            // If no best individual yet, try to get any individual's weights (once per interval)
            var population = coordinator.CurrentPopulation?.Individuals;
            if (population != null && population.Any())
            {
                var anyIndividual = population.FirstOrDefault();
                if (anyIndividual?.Genome != null)
                {
                    if (_cachedIndividualId != anyIndividual.Id)
                    {
                        Console.WriteLine($"Using weights from random individual {anyIndividual.Id} (Gen: {anyIndividual.Generation}) - no fitness data yet");
                    }
                    _cachedIndividualId = anyIndividual.Id;
                    _cachedWeights = anyIndividual.Genome;
                    _lastWeightsUpdate = DateTime.UtcNow;
                    return _cachedWeights;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error accessing evolution coordinator: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Loads static weights from file as fallback
    /// </summary>
    private static void LoadStaticWeights()
    {
        try
        {
            string solutionDir = AppContext.BaseDirectory.Substring(0, AppContext.BaseDirectory.IndexOf("Bots"));
            string weightsPath = Path.Combine(solutionDir, "Bots", "ClingyHeuroBot2", "heuristic-weights.json");

            if (File.Exists(weightsPath))
            {
                var weightsJson = File.ReadAllText(weightsPath);
                _staticWeights = JsonSerializer.Deserialize<HeuristicWeights>(weightsJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new HeuristicWeights();
                Console.WriteLine("Static heuristic weights loaded successfully as fallback");
            }
            else
            {
                _staticWeights = new HeuristicWeights();
                Console.WriteLine("No static weights file found, using default weights as fallback");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading static weights: {ex.Message}");
            _staticWeights = new HeuristicWeights();
        }
    }

    /// <summary>
    /// Forces a reload of evolved weights
    /// </summary>
    public static void RefreshWeights()
    {
        lock (_lock)
        {
            _lastWeightsUpdate = DateTime.MinValue; // Force update on next access
        }
    }

    /// <summary>
    /// Gets statistics about current weight source
    /// </summary>
    public static string GetWeightSourceInfo()
    {
        try
        {
            var evolvedWeights = GetEvolvedWeights();
            if (evolvedWeights != null)
            {
                var coordinator = ClingyHeuroBot2.EvolutionCoordinator.Instance;
                var bestIndividual = coordinator.GetBestIndividual();
                if (bestIndividual != null)
                {
                    return $"Evolved weights from Gen {bestIndividual.Generation}, Fitness {bestIndividual.Fitness:F2}, {bestIndividual.GamesPlayed} games";
                }
                return "Evolved weights from random individual (no fitness yet)";
            }
            return "Static weights from heuristic-weights.json";
        }
        catch
        {
            return "Default weights (fallback)";
        }
    }

    /// <summary>
    /// Validates that weights are not null and contain reasonable values
    /// </summary>
    private static bool ValidateWeights(HeuristicWeights weights)
    {
        if (weights == null)
        {
            Console.WriteLine("Weight validation failed: weights are null");
            return false;
        }

        try
        {
            // Check a few key properties to ensure they're accessible
            var testValue = weights.DistanceToGoal + weights.PathSafety + weights.CaptureAvoidance;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Weight validation failed: {ex.Message}");
            return false;
        }
    }
}
