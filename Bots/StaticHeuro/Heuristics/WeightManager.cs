using Marvijo.Zooscape.Bots.Common.Models;
using System.IO;
using System.Text.Json;
using StaticHeuro;

namespace StaticHeuro.Heuristics;

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
    /// Gets the static weights from heuristic-weights.json (evolution system disabled)
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

                // Always return static weights - evolution system disabled
                var staticWeights = _staticWeights ?? new HeuristicWeights();
                Console.WriteLine("Using static weights from heuristic-weights.json (evolution disabled)");
                return staticWeights;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading static weights: {ex.Message}");
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
    /// Gets evolved weights from the evolution coordinator (DISABLED - always returns null)
    /// </summary>
    private static HeuristicWeights? GetEvolvedWeights()
    {
        // Evolution system disabled - always return null to force static weights
        return null;
    }

    /// <summary>
    /// Loads static weights from file as fallback
    /// </summary>
    private static void LoadStaticWeights()
    {
        try
        {
            if (File.Exists("heuristic-weights.json"))
            {
                var weightsJson = File.ReadAllText("heuristic-weights.json");
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
                var coordinator = EvolutionCoordinator.Instance;
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
