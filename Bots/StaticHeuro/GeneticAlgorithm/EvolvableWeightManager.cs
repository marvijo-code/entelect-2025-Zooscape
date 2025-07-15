#pragma warning disable SKEXP0110

using ClingyHeuroBot2.Heuristics;
using Marvijo.Zooscape.Bots.Common.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ClingyHeuroBot2.GeneticAlgorithm;

/// <summary>
/// Manages heuristic weights for evolutionary bots with runtime updates and validation
/// </summary>
public class EvolvableWeightManager : IDisposable
{
    private readonly ILogger<EvolvableWeightManager> _logger;
    private readonly string _weightsDirectory;
    private readonly object _weightsLock = new();
    private readonly Dictionary<Guid, HeuristicWeights> _individualWeights = new();
    private readonly Timer _autoSaveTimer;

    public event EventHandler<WeightsUpdatedEventArgs>? WeightsUpdated;

    public EvolvableWeightManager(ILogger<EvolvableWeightManager> logger, string? weightsDirectory = null)
    {
        _logger = logger;
        _weightsDirectory = weightsDirectory ?? Path.Combine(Environment.CurrentDirectory, "evolved-weights");
        
        Directory.CreateDirectory(_weightsDirectory);
        
        // Auto-save timer every 5 minutes
        _autoSaveTimer = new Timer(AutoSaveCallback, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        
        _logger.LogInformation($"EvolvableWeightManager initialized with directory: {_weightsDirectory}");
    }

    /// <summary>
    /// Gets the current weights for a specific individual, falling back to base weights if not found
    /// </summary>
    public HeuristicWeights GetWeights(Guid? individualId = null)
    {
        lock (_weightsLock)
        {
            if (individualId.HasValue && _individualWeights.TryGetValue(individualId.Value, out var weights))
            {
                return CloneWeights(weights);
            }

            // Fallback to base weights
            return WeightManager.Instance;
        }
    }

    /// <summary>
    /// Sets weights for a specific individual
    /// </summary>
    public void SetWeights(Guid individualId, HeuristicWeights weights)
    {
        if (weights == null)
            throw new ArgumentNullException(nameof(weights));

        var validatedWeights = ValidateAndSanitizeWeights(weights);
        
        lock (_weightsLock)
        {
            _individualWeights[individualId] = CloneWeights(validatedWeights);
        }

        _logger.LogDebug($"Set weights for individual {individualId}");
        WeightsUpdated?.Invoke(this, new WeightsUpdatedEventArgs(individualId, validatedWeights));
    }

    /// <summary>
    /// Updates weights for an individual from the genetic algorithm
    /// </summary>
    public void UpdateFromIndividual(Individual individual)
    {
        if (individual?.Genome == null)
        {
            _logger.LogWarning("Cannot update weights from null individual or genome");
            return;
        }

        SetWeights(individual.Id, individual.Genome);
        
        // Also save to file for persistence
        _ = Task.Run(() => SaveIndividualWeightsAsync(individual));
    }

    /// <summary>
    /// Loads weights for multiple individuals from the evolution data
    /// </summary>
    public async Task LoadIndividualWeightsAsync(IEnumerable<Individual> individuals)
    {
        var tasks = individuals.Select(LoadIndividualWeightsAsync);
        await Task.WhenAll(tasks);
        
        _logger.LogInformation($"Loaded weights for {individuals.Count()} individuals");
    }

    /// <summary>
    /// Loads weights for a specific individual from file
    /// </summary>
    public async Task LoadIndividualWeightsAsync(Individual individual)
    {
        try
        {
            var weightsPath = GetIndividualWeightsPath(individual.Id);
            if (File.Exists(weightsPath))
            {
                var json = await File.ReadAllTextAsync(weightsPath);
                var weights = JsonSerializer.Deserialize<HeuristicWeights>(json);
                
                if (weights != null)
                {
                    SetWeights(individual.Id, weights);
                    
                    // Also update the individual's genome if it's different
                    if (!AreWeightsEqual(individual.Genome, weights))
                    {
                        individual.Genome = CloneWeights(weights);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Failed to load weights for individual {individual.Id}");
        }
    }

    /// <summary>
    /// Saves weights for a specific individual to file
    /// </summary>
    public async Task SaveIndividualWeightsAsync(Individual individual)
    {
        try
        {
            var weightsPath = GetIndividualWeightsPath(individual.Id);
            var weightsData = new
            {
                IndividualId = individual.Id,
                Generation = individual.Generation,
                Fitness = individual.Fitness,
                LastUpdated = DateTime.UtcNow,
                Weights = individual.Genome
            };

            var json = JsonSerializer.Serialize(weightsData, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(weightsPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to save weights for individual {individual.Id}");
        }
    }

    /// <summary>
    /// Saves all current individual weights to files
    /// </summary>
    public async Task SaveAllWeightsAsync()
    {
        var tasks = new List<Task>();
        
        lock (_weightsLock)
        {
            foreach (var kvp in _individualWeights)
            {
                var individualId = kvp.Key;
                var weights = kvp.Value;
                
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var weightsPath = GetIndividualWeightsPath(individualId);
                        var weightsData = new
                        {
                            IndividualId = individualId,
                            LastUpdated = DateTime.UtcNow,
                            Weights = weights
                        };

                        var json = JsonSerializer.Serialize(weightsData, new JsonSerializerOptions 
                        { 
                            WriteIndented = true 
                        });
                        
                        await File.WriteAllTextAsync(weightsPath, json);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to save weights for individual {individualId}");
                    }
                }));
            }
        }

        await Task.WhenAll(tasks);
        _logger.LogInformation($"Saved weights for {tasks.Count} individuals");
    }

    /// <summary>
    /// Removes weights for an individual (cleanup)
    /// </summary>
    public void RemoveIndividualWeights(Guid individualId)
    {
        lock (_weightsLock)
        {
            _individualWeights.Remove(individualId);
        }

        // Also remove the file
        _ = Task.Run(() =>
        {
            try
            {
                var weightsPath = GetIndividualWeightsPath(individualId);
                if (File.Exists(weightsPath))
                {
                    File.Delete(weightsPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to delete weights file for individual {individualId}");
            }
        });

        _logger.LogDebug($"Removed weights for individual {individualId}");
    }

    /// <summary>
    /// Gets the count of managed individuals
    /// </summary>
    public int GetManagedIndividualCount()
    {
        lock (_weightsLock)
        {
            return _individualWeights.Count;
        }
    }

    /// <summary>
    /// Gets all managed individual IDs
    /// </summary>
    public List<Guid> GetManagedIndividualIds()
    {
        lock (_weightsLock)
        {
            return _individualWeights.Keys.ToList();
        }
    }

    /// <summary>
    /// Validates and sanitizes weights to ensure they're within acceptable bounds
    /// </summary>
    private HeuristicWeights ValidateAndSanitizeWeights(HeuristicWeights weights)
    {
        var sanitized = CloneWeights(weights);
        bool wasModified = false;

        var properties = typeof(HeuristicWeights).GetProperties()
            .Where(p => p.CanWrite && (p.PropertyType == typeof(decimal) || p.PropertyType == typeof(int)));

        foreach (var property in properties)
        {
            if (property.PropertyType == typeof(decimal))
            {
                var value = (decimal)property.GetValue(sanitized);
                var clampedValue = Math.Max(-1000m, Math.Min(1000m, value));
                
                if (value != clampedValue)
                {
                    property.SetValue(sanitized, clampedValue);
                    wasModified = true;
                }
            }
            else if (property.PropertyType == typeof(int))
            {
                var value = (int)property.GetValue(sanitized);
                var clampedValue = Math.Max(0, Math.Min(10000, value));
                
                if (value != clampedValue)
                {
                    property.SetValue(sanitized, clampedValue);
                    wasModified = true;
                }
            }
        }

        if (wasModified)
        {
            _logger.LogDebug("Weights were sanitized to fit within acceptable bounds");
        }

        return sanitized;
    }

    /// <summary>
    /// Creates a deep copy of weights
    /// </summary>
    private HeuristicWeights CloneWeights(HeuristicWeights weights)
    {
        var json = JsonSerializer.Serialize(weights);
        return JsonSerializer.Deserialize<HeuristicWeights>(json) ?? new HeuristicWeights();
    }

    /// <summary>
    /// Compares two weight objects for equality
    /// </summary>
    private bool AreWeightsEqual(HeuristicWeights weights1, HeuristicWeights weights2)
    {
        if (weights1 == null || weights2 == null)
            return weights1 == weights2;

        var json1 = JsonSerializer.Serialize(weights1);
        var json2 = JsonSerializer.Serialize(weights2);
        
        return json1 == json2;
    }

    /// <summary>
    /// Gets the file path for an individual's weights
    /// </summary>
    private string GetIndividualWeightsPath(Guid individualId)
    {
        return Path.Combine(_weightsDirectory, $"weights-{individualId:N}.json");
    }

    /// <summary>
    /// Auto-save callback for the timer
    /// </summary>
    private void AutoSaveCallback(object? state)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await SaveAllWeightsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-save of weights failed");
            }
        });
    }

    /// <summary>
    /// Creates a backup of all current weights
    /// </summary>
    public async Task CreateBackupAsync(string? backupName = null)
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
            var backupDir = Path.Combine(_weightsDirectory, "backups", backupName ?? $"backup_{timestamp}");
            
            Directory.CreateDirectory(backupDir);

            // Copy all individual weight files
            var weightFiles = Directory.GetFiles(_weightsDirectory, "weights-*.json");
            var copyTasks = weightFiles.Select(file => Task.Run(() =>
            {
                var fileName = Path.GetFileName(file);
                var destPath = Path.Combine(backupDir, fileName);
                File.Copy(file, destPath, overwrite: true);
            }));

            await Task.WhenAll(copyTasks);

            // Create backup metadata
            var metadata = new
            {
                BackupName = backupName ?? $"backup_{timestamp}",
                CreatedAt = DateTime.UtcNow,
                IndividualCount = weightFiles.Length,
                BackupType = "EvolvableWeights"
            };

            var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(backupDir, "backup-metadata.json"), metadataJson);

            _logger.LogInformation($"Created weights backup: {backupDir} with {weightFiles.Length} files");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create weights backup");
        }
    }

    /// <summary>
    /// Restores weights from a backup
    /// </summary>
    public async Task RestoreFromBackupAsync(string backupName)
    {
        try
        {
            var backupDir = Path.Combine(_weightsDirectory, "backups", backupName);
            if (!Directory.Exists(backupDir))
            {
                throw new DirectoryNotFoundException($"Backup directory not found: {backupDir}");
            }

            var weightFiles = Directory.GetFiles(backupDir, "weights-*.json");
            var restoreTasks = weightFiles.Select(file => Task.Run(async () =>
            {
                var fileName = Path.GetFileName(file);
                var destPath = Path.Combine(_weightsDirectory, fileName);
                File.Copy(file, destPath, overwrite: true);

                // Also load into memory
                var json = await File.ReadAllTextAsync(file);
                var data = JsonSerializer.Deserialize<JsonElement>(json);
                
                if (data.TryGetProperty("IndividualId", out var idElement) &&
                    data.TryGetProperty("Weights", out var weightsElement))
                {
                    var individualId = idElement.GetGuid();
                    var weights = JsonSerializer.Deserialize<HeuristicWeights>(weightsElement.GetRawText());
                    
                    if (weights != null)
                    {
                        SetWeights(individualId, weights);
                    }
                }
            }));

            await Task.WhenAll(restoreTasks);
            _logger.LogInformation($"Restored weights from backup: {backupName} with {weightFiles.Length} files");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to restore weights from backup: {backupName}");
            throw;
        }
    }

    /// <summary>
    /// Gets statistics about the managed weights
    /// </summary>
    public EvolvableWeightsStatistics GetStatistics()
    {
        lock (_weightsLock)
        {
            return new EvolvableWeightsStatistics
            {
                ManagedIndividualCount = _individualWeights.Count,
                WeightsDirectory = _weightsDirectory,
                LastAutoSave = DateTime.UtcNow // Approximation, would need to track this properly
            };
        }
    }

    public void Dispose()
    {
        _autoSaveTimer?.Dispose();
        
        // Save all weights before disposing
        try
        {
            SaveAllWeightsAsync().Wait(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save weights during disposal");
        }
    }
}

/// <summary>
/// Event arguments for weight updates
/// </summary>
public class WeightsUpdatedEventArgs : EventArgs
{
    public Guid IndividualId { get; }
    public HeuristicWeights UpdatedWeights { get; }
    public DateTime Timestamp { get; }

    public WeightsUpdatedEventArgs(Guid individualId, HeuristicWeights updatedWeights)
    {
        IndividualId = individualId;
        UpdatedWeights = updatedWeights;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Statistics for evolvable weights management
/// </summary>
public class EvolvableWeightsStatistics
{
    public int ManagedIndividualCount { get; set; }
    public string WeightsDirectory { get; set; } = string.Empty;
    public DateTime LastAutoSave { get; set; }

    public override string ToString()
    {
        return $"Evolvable Weights - Individuals: {ManagedIndividualCount}, Directory: {WeightsDirectory}";
    }
} 