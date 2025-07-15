#pragma warning disable SKEXP0110

using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ClingyHeuroBot2.GeneticAlgorithm;

/// <summary>
/// Tracks and manages high scores and performance statistics for evolved bots
/// </summary>
public class HighScoreTracker
{
    private readonly ILogger<HighScoreTracker> _logger;
    private readonly string _highScoresPath;
    private readonly string _statisticsPath;
    private readonly string _backupDirectory;
    private readonly object _fileLock = new();
    private readonly Timer _backupTimer;
    private List<HighScoreEntry> _highScores = new();

    public HighScoreTracker(ILogger<HighScoreTracker> logger, string? dataDirectory = null)
    {
        _logger = logger;
        var baseDirectory = dataDirectory ?? Path.Combine(Environment.CurrentDirectory, "evolution-data");
        
        _highScoresPath = Path.Combine(baseDirectory, "high-scores.json");
        _statisticsPath = Path.Combine(baseDirectory, "statistics.json");
        _backupDirectory = Path.Combine(baseDirectory, "high-score-backups");
        
        Directory.CreateDirectory(baseDirectory);
        Directory.CreateDirectory(_backupDirectory);
        
        // Create initial files if they don't exist
        InitializeFiles();
        
        // Backup timer - creates backups every hour
        _backupTimer = new Timer(CreateBackupCallback, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        
        _logger.LogInformation($"HighScoreTracker initialized with path: {_highScoresPath}");
        
        LoadHighScores();
    }

    /// <summary>
    /// Records a new performance entry
    /// </summary>
    public async Task RecordPerformanceAsync(Individual individual, GamePerformance performance, int generation)
    {
        var entry = new HighScoreEntry
        {
            Timestamp = DateTime.UtcNow,
            IndividualId = individual.Id,
            Score = performance.Score,
            Fitness = performance.Fitness,
            Generation = generation,
            Rank = performance.FinalRank,
            TotalPlayers = performance.TotalPlayers
        };
        lock (_fileLock)
        {
            _highScores.Add(entry);
            _highScores = _highScores.OrderByDescending(e => e.Score).Take(1000).ToList(); // Keep top 1000
            SaveHighScores();
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the top N high score entries
    /// </summary>
    public List<HighScoreEntry> GetHighScores(int top = 10)
    {
        lock (_fileLock)
        {
            return _highScores.OrderByDescending(e => e.Score).Take(top).ToList();
        }
    }

    /// <summary>
    /// Gets statistics for a specific generation
    /// </summary>
    public async Task<GenerationStatistics?> GetGenerationStatisticsAsync(int generation)
    {
        try
        {
            var entries = await ParseHighScoreEntriesAsync();
            var generationEntries = entries.Where(e => e.Generation == generation).ToList();
            
            if (!generationEntries.Any())
                return null;
                
            return new GenerationStatistics
            {
                Generation = generation,
                EntryCount = generationEntries.Count,
                AverageScore = generationEntries.Average(e => e.Score),
                BestScore = generationEntries.Max(e => e.Score),
                WorstScore = generationEntries.Min(e => e.Score),
                AverageFitness = generationEntries.Average(e => e.Fitness),
                BestFitness = generationEntries.Max(e => e.Fitness),
                AverageSurvivalTime = generationEntries.Average(e => e.SurvivalTime),
                BestSurvivalTime = generationEntries.Max(e => e.SurvivalTime),
                CaptureStats = new CaptureStatistics
                {
                    AverageCaptures = generationEntries.Average(e => e.Captures),
                    MinCaptures = generationEntries.Min(e => e.Captures),
                    MaxCaptures = generationEntries.Max(e => e.Captures),
                    NoCapturePercentage = generationEntries.Count(e => e.Captures == 0) * 100.0 / generationEntries.Count
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get statistics for generation {generation}");
            return null;
        }
    }

    /// <summary>
    /// Gets overall statistics across all generations
    /// </summary>
    public async Task<OverallStatistics> GetOverallStatisticsAsync()
    {
        try
        {
            var entries = await ParseHighScoreEntriesAsync();
            
            if (!entries.Any())
            {
                return new OverallStatistics();
            }
            
            var generationStats = entries.GroupBy(e => e.Generation)
                .Select(g => new
                {
                    Generation = g.Key,
                    Count = g.Count(),
                    AvgFitness = g.Average(e => e.Fitness),
                    BestFitness = g.Max(e => e.Fitness)
                })
                .OrderBy(g => g.Generation)
                .ToList();
            
            return new OverallStatistics
            {
                TotalEntries = entries.Count,
                GenerationCount = generationStats.Count,
                FirstGeneration = generationStats.FirstOrDefault()?.Generation ?? 0,
                LatestGeneration = generationStats.LastOrDefault()?.Generation ?? 0,
                
                AllTimeBestScore = entries.Max(e => e.Score),
                AllTimeBestFitness = entries.Max(e => e.Fitness),
                AllTimeBestSurvival = entries.Max(e => e.SurvivalTime),
                
                AverageScoreOverall = entries.Average(e => e.Score),
                AverageFitnessOverall = entries.Average(e => e.Fitness),
                AverageSurvivalOverall = entries.Average(e => e.SurvivalTime),
                
                EvolutionProgress = CalculateEvolutionProgress(generationStats.Cast<dynamic>().ToList()),
                TopPerformers = GetHighScores(5)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get overall statistics");
            return new OverallStatistics();
        }
    }

    /// <summary>
    /// Creates a formatted report of current statistics
    /// </summary>
    public async Task<string> GenerateReportAsync()
    {
        try
        {
            var overall = await GetOverallStatisticsAsync();
            var topScores = GetHighScores(10);
            
            var report = new System.Text.StringBuilder();
            
            report.AppendLine("=== EVOLVED BOT PERFORMANCE REPORT ===");
            report.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            report.AppendLine();
            
            report.AppendLine("OVERALL STATISTICS:");
            report.AppendLine($"  Total Entries: {overall.TotalEntries}");
            report.AppendLine($"  Generations: {overall.FirstGeneration} - {overall.LatestGeneration} ({overall.GenerationCount} total)");
            report.AppendLine($"  All-Time Best Score: {overall.AllTimeBestScore}");
            report.AppendLine($"  All-Time Best Fitness: {overall.AllTimeBestFitness:F2}");
            report.AppendLine($"  All-Time Best Survival: {overall.AllTimeBestSurvival:F1}s");
            report.AppendLine($"  Evolution Progress: {overall.EvolutionProgress:F2}% improvement");
            report.AppendLine();
            
            report.AppendLine("TOP 10 PERFORMERS:");
            for (int i = 0; i < topScores.Count; i++)
            {
                var entry = topScores[i];
                report.AppendLine($"  {i + 1,2}. Gen:{entry.Generation:D4} Score:{entry.Score,6} Fitness:{entry.Fitness,6:F2} " +
                                $"Survival:{entry.SurvivalTime,5:F1}s Captures:{entry.Captures} Method:{entry.Method}");
            }
            
            return report.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate report");
            return $"Error generating report: {ex.Message}";
        }
    }

    /// <summary>
    /// Exports all data to a structured format
    /// </summary>
    public async Task ExportDataAsync(string filePath, string format = "json")
    {
        try
        {
            var entries = await ParseHighScoreEntriesAsync();
            var overall = await GetOverallStatisticsAsync();
            
            var exportData = new
            {
                ExportTimestamp = DateTime.UtcNow,
                OverallStatistics = overall,
                Entries = entries.OrderByDescending(e => e.Fitness).ToList()
            };
            
            string content = format.ToLower() switch
            {
                "json" => JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true }),
                "csv" => ConvertToCsv(entries),
                _ => throw new ArgumentException($"Unsupported format: {format}")
            };
            
            await File.WriteAllTextAsync(filePath, content);
            _logger.LogInformation($"Exported data to {filePath} in {format} format");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to export data to {filePath}");
            throw;
        }
    }

    /// <summary>
    /// Parses high score entries from the file
    /// </summary>
    private async Task<List<HighScoreEntry>> ParseHighScoreEntriesAsync()
    {
        var entries = new List<HighScoreEntry>();
        
        if (!File.Exists(_highScoresPath))
            return entries;
            
        var json = await File.ReadAllTextAsync(_highScoresPath);
        entries = JsonSerializer.Deserialize<List<HighScoreEntry>>(json) ?? new List<HighScoreEntry>();
        
        return entries;
    }

    /// <summary>
    /// Calculates evolution progress percentage
    /// </summary>
    private double CalculateEvolutionProgress(List<dynamic> generationStats)
    {
        if (generationStats.Count < 2) return 0;
        
        var firstGenAvg = (double)generationStats.First().AvgFitness;
        var lastGenAvg = (double)generationStats.Last().AvgFitness;
        
        return firstGenAvg > 0 ? ((lastGenAvg - firstGenAvg) / firstGenAvg) * 100 : 0;
    }

    /// <summary>
    /// Converts entries to CSV format
    /// </summary>
    private string ConvertToCsv(List<HighScoreEntry> entries)
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Timestamp,Generation,IndividualId,Score,Fitness,SurvivalTime,Captures,Rank,TotalPlayers,Method");
        
        foreach (var entry in entries)
        {
            csv.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss},{entry.Generation},{entry.IndividualId}," +
                          $"{entry.Score},{entry.Fitness:F2},{entry.SurvivalTime:F1},{entry.Captures}," +
                          $"{entry.Rank},{entry.TotalPlayers},{entry.Method}");
        }
        
        return csv.ToString();
    }

    /// <summary>
    /// Initializes files if they don't exist
    /// </summary>
    private void InitializeFiles()
    {
        if (!File.Exists(_highScoresPath))
        {
            var header = "# High Scores for Evolved ClingyHeuroBot2\n" +
                        "# Format: [Timestamp] Gen:XXXX | ID:individual-id | Score:XXXX | Fitness:XX.XX | Survival:XXXs | Captures:X | Rank:X/X | Method:EvolutionMethod\n" +
                        "# Generated automatically by the Genetic Algorithm Evolution System\n";
            File.WriteAllText(_highScoresPath, header);
        }
    }

    /// <summary>
    /// Creates a backup of the high scores file
    /// </summary>
    private void CreateBackupCallback(object? state)
    {
        try
        {
            if (!File.Exists(_highScoresPath)) return;
            
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
            var backupPath = Path.Combine(_backupDirectory, $"high-scores_{timestamp}.json");
            
            File.Copy(_highScoresPath, backupPath);
            
            // Clean up old backups (keep last 24)
            var backupFiles = Directory.GetFiles(_backupDirectory, "high-scores_*.json")
                                     .OrderByDescending(f => File.GetCreationTime(f))
                                     .Skip(24);
            
            foreach (var oldBackup in backupFiles)
            {
                try { File.Delete(oldBackup); } catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create high scores backup");
        }
    }

    public void Dispose()
    {
        _backupTimer?.Dispose();
    }

    private void LoadHighScores()
    {
        lock (_fileLock)
        {
            if (File.Exists(_highScoresPath))
            {
                try
                {
                    var json = File.ReadAllText(_highScoresPath);
                    _highScores = JsonSerializer.Deserialize<List<HighScoreEntry>>(json) ?? new List<HighScoreEntry>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to load high scores: {ex.Message}");
                    _highScores = new List<HighScoreEntry>();
                }
            }
            else
            {
                _highScores = new List<HighScoreEntry>();
            }
        }
    }

    private void SaveHighScores()
    {
        lock (_fileLock)
        {
            try
            {
                var json = JsonSerializer.Serialize(_highScores, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_highScoresPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to save high scores: {ex.Message}");
            }
        }
    }
}

// Data classes for statistics and entries

public class HighScoreEntry
{
    public DateTime Timestamp { get; set; }
    public int Generation { get; set; }
    public Guid IndividualId { get; set; }
    public int Score { get; set; }
    public double Fitness { get; set; }
    public double SurvivalTime { get; set; }
    public int Captures { get; set; }
    public int Rank { get; set; }
    public int TotalPlayers { get; set; }
    public string Method { get; set; } = string.Empty;
}

public class GenerationStatistics
{
    public int Generation { get; set; }
    public int EntryCount { get; set; }
    public double AverageScore { get; set; }
    public int BestScore { get; set; }
    public int WorstScore { get; set; }
    public double AverageFitness { get; set; }
    public double BestFitness { get; set; }
    public double AverageSurvivalTime { get; set; }
    public double BestSurvivalTime { get; set; }
    public CaptureStatistics CaptureStats { get; set; } = new();
}

public class CaptureStatistics
{
    public double AverageCaptures { get; set; }
    public int MinCaptures { get; set; }
    public int MaxCaptures { get; set; }
    public double NoCapturePercentage { get; set; }
}

public class OverallStatistics
{
    public int TotalEntries { get; set; }
    public int GenerationCount { get; set; }
    public int FirstGeneration { get; set; }
    public int LatestGeneration { get; set; }
    public int AllTimeBestScore { get; set; }
    public double AllTimeBestFitness { get; set; }
    public double AllTimeBestSurvival { get; set; }
    public double AverageScoreOverall { get; set; }
    public double AverageFitnessOverall { get; set; }
    public double AverageSurvivalOverall { get; set; }
    public double EvolutionProgress { get; set; }
    public List<HighScoreEntry> TopPerformers { get; set; } = new();
} 