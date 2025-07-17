using FunctionalTests.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FunctionalTests.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaderboardController : ControllerBase
{
    private readonly string _logsDir;
    private readonly Services.CacheService _cache;
    private readonly Serilog.ILogger _logger;

    public LeaderboardController(IConfiguration configuration, Services.CacheService cache, Serilog.ILogger logger)
    {
        _logsDir = configuration["LogsDir"] ?? "C:\\dev\\2025-Zooscape\\logs";
        _cache = cache;
        _logger = logger.ForContext<LeaderboardController>();
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetLeaderboardStats()
    {
        // Fast-path: serve cached value when available
        if (_cache.TryGet("leaderboard-stats", out List<BotStats>? leaderboard) && leaderboard is not null)
        {
            return Ok(leaderboard);
        }

        // For cold cache, return a lightweight response or trigger background refresh
        _logger.Warning("Cache miss for leaderboard-stats endpoint. Triggering background refresh.");
        
        // Try to get a stale cached value first (if any)
        if (_cache.TryGet("leaderboard-stats-stale", out List<BotStats>? staleLeaderboard) && staleLeaderboard is not null)
        {
            _logger.Information("Serving stale leaderboard data while refreshing cache");
            // Trigger async refresh without blocking
            _ = Task.Run(async () => await RefreshCacheAsync());
            return Ok(staleLeaderboard);
        }

        // Last resort: calculate on-demand with timeout protection
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var result = await GetLeaderboardStatsWithTimeout(cts.Token);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.Error("Leaderboard stats calculation timed out after 10 seconds");
            return StatusCode(503, new { error = "Service temporarily unavailable. Please try again later." });
        }
    }

    [HttpGet("stats-full")]
    public async Task<IActionResult> GetLeaderboardStatsFull()
    {
        var leaderboard = await _cache.GetOrCreateAsync("leaderboard-stats", async () =>
        {
            _logger.Information("Cache miss for leaderboard-stats. Recalculating...");

            var botStats = new Dictionary<string, BotStats>();
            var gameRunDirs = Directory.GetDirectories(_logsDir);

            _logger.Information("Found {RunCount} game run directories", gameRunDirs.Length);

            foreach (var runDir in gameRunDirs)
            {
                Console.WriteLine($"Processing run directory: {runDir}");
                try
                {
                    var logFiles = Directory.GetFiles(runDir, "*.json")
                                    .Select(f => new { Path = f, Number = int.Parse(Path.GetFileNameWithoutExtension(f)) })
                                    .OrderBy(f => f.Number)
                                    .ToList();

                    if (!logFiles.Any()) continue;

                    // Determine max capture count per bot across all ticks in this run
                    var perBotMaxCaptures = new Dictionary<string, int>();

                    foreach (var log in logFiles)
                    {
                        var content = await System.IO.File.ReadAllTextAsync(log.Path);
                        var gs = JsonSerializer.Deserialize<JsonElement>(content);
                        if (!gs.TryGetProperty("Animals", out var animalsEl)) continue;

                        foreach (var animalJson in animalsEl.EnumerateArray())
                        {
                            var nickname = animalJson.TryGetProperty("Nickname", out var nNick) ? nNick.GetString() : null;
                            if (nickname is null) continue;
                            var captures = animalJson.TryGetProperty("CapturedCounter", out var ccEl) && ccEl.TryGetInt32(out var ccVal) ? ccVal : 0;

                            if (!perBotMaxCaptures.TryGetValue(nickname, out var existing) || captures > existing)
                            {
                                perBotMaxCaptures[nickname] = captures;
                            }
                        }
                    }

                    // We still need score / ranking info – get it from final tick for podium positions
                    var finalLogPath = logFiles.Last().Path;
                    var fileContent = await System.IO.File.ReadAllTextAsync(finalLogPath);
                    var gameState = JsonSerializer.Deserialize<JsonElement>(fileContent);

                    if (!gameState.TryGetProperty("Animals", out var animalsElement))
                    {
                        continue;
                    }

                    var animals = animalsElement.EnumerateArray()
                        .Select(animal => new
                        {
                            Id = animal.TryGetProperty("Id", out var id) ? id.GetString() : null,
                            Nickname = animal.TryGetProperty("Nickname", out var nick) ? nick.GetString() : null,
                            Score = animal.TryGetProperty("Score", out var s) && s.TryGetInt32(out var score) ? score : 0,
                            Captures = animal.TryGetProperty("CapturedCounter", out var cc) && cc.TryGetInt32(out var captures) ? captures : 0
                        })
                        .OrderByDescending(a => a.Score)
                        .ToList();

                    if (!animals.Any()) continue;

                    foreach (var (animal, index) in animals.Select((value, i) => (value, i)))
                    {
                        if (animal.Nickname is null) continue;
                        if (!botStats.TryGetValue(animal.Nickname, out var stats))
                        {
                            stats = new BotStats
                            {
                                Nickname = animal.Nickname,
                                Id = animal.Id,
                                Wins = 0,
                                SecondPlaces = 0,
                                GamesPlayed = 0,
                                TotalCaptures = 0
                            };
                            botStats[animal.Nickname] = stats;
                        }

                        stats.GamesPlayed++;
                        var maxCaptures = perBotMaxCaptures.TryGetValue(animal.Nickname, out var mc) ? mc : 0;
                        stats.TotalCaptures += maxCaptures;
                        if (index == 0) stats.Wins++;
                        if (index == 1) stats.SecondPlaces++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error processing game run {RunDir}", runDir);
                }
            }

            // Calculate average captures for serialization
                foreach (var bs in botStats.Values)
                {
                    bs.AverageCaptures = bs.GamesPlayed > 0 ? (double)bs.TotalCaptures / bs.GamesPlayed : 0;
                }

                return botStats.Values
                            .OrderByDescending(b => b.Wins)
                            .ThenByDescending(b => b.SecondPlaces)
                            .ThenByDescending(b => b.GamesPlayed)
                            .ToList();
            }, TimeSpan.FromMinutes(2));


        
        return Ok(leaderboard);
    }

    private async Task<List<BotStats>> GetLeaderboardStatsWithTimeout(CancellationToken cancellationToken)
    {
        var leaderboard = await _cache.GetOrCreateAsync("leaderboard-stats", async () =>
        {
            _logger.Information("Cache miss for leaderboard-stats. Recalculating with timeout protection...");
            return await CalculateStatsOptimized(cancellationToken);
        }, TimeSpan.FromMinutes(2));

        return leaderboard;
    }

    private async Task RefreshCacheAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var stats = await CalculateStatsOptimized(cts.Token);
            
            // Store both fresh and stale versions
            _cache.Set("leaderboard-stats", stats, TimeSpan.FromMinutes(2));
            _cache.Set("leaderboard-stats-stale", stats, TimeSpan.FromMinutes(10));
            
            _logger.Information("Background cache refresh completed. Bots: {Count}", stats.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to refresh leaderboard cache in background");
        }
    }

    private async Task<List<BotStats>> CalculateStatsOptimized(CancellationToken cancellationToken)
    {
        var botStats = new Dictionary<string, BotStats>();
        
        if (!Directory.Exists(_logsDir))
        {
            _logger.Warning("Logs directory not found at {LogsDir}", _logsDir);
            return new List<BotStats>();
        }

        var gameRunDirs = Directory.GetDirectories(_logsDir);
        _logger.Information("Processing {RunCount} game run directories", gameRunDirs.Length);

        // Process directories in parallel for better performance
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
        var tasks = gameRunDirs.Select(async runDir =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await ProcessGameRunDirectory(runDir, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        
        // Aggregate results from all directories
        foreach (var runResult in results.Where(r => r != null))
        {
            foreach (var (nickname, stats) in runResult!)
            {
                if (!botStats.TryGetValue(nickname, out var existingStats))
                {
                    existingStats = new BotStats
                    {
                        Nickname = nickname,
                        Id = stats.Id,
                        Wins = 0,
                        SecondPlaces = 0,
                        GamesPlayed = 0,
                        TotalCaptures = 0
                    };
                    botStats[nickname] = existingStats;
                }

                existingStats.GamesPlayed += stats.GamesPlayed;
                existingStats.Wins += stats.Wins;
                existingStats.SecondPlaces += stats.SecondPlaces;
                existingStats.TotalCaptures += stats.TotalCaptures;
            }
        }

        // Calculate averages
        foreach (var bs in botStats.Values)
        {
            bs.AverageCaptures = bs.GamesPlayed > 0 ? (double)bs.TotalCaptures / bs.GamesPlayed : 0;
        }

        return botStats.Values
            .OrderByDescending(b => b.Wins)
            .ThenByDescending(b => b.SecondPlaces)
            .ThenByDescending(b => b.GamesPlayed)
            .ToList();
    }

    private async Task<Dictionary<string, BotStats>?> ProcessGameRunDirectory(string runDir, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var logFiles = Directory.GetFiles(runDir, "*.json")
                .Select(f => new { Path = f, Number = int.Parse(Path.GetFileNameWithoutExtension(f)) })
                .OrderBy(f => f.Number)
                .ToList();

            if (!logFiles.Any()) return null;

            // Optimization: Only read final file for ranking and a sample of files for max captures
            var finalLogPath = logFiles.Last().Path;
            var sampleFiles = logFiles.Count > 10 
                ? logFiles.Where((f, i) => i % Math.Max(1, logFiles.Count / 10) == 0).ToList()
                : logFiles;

            // Get max captures from sample files (optimization)
            var perBotMaxCaptures = new Dictionary<string, int>();
            foreach (var logFile in sampleFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var content = await File.ReadAllTextAsync(logFile.Path, cancellationToken);
                if (string.IsNullOrEmpty(content)) continue;
                
                try
                {
                    var gs = JsonSerializer.Deserialize<JsonElement>(content);
                    if (!gs.TryGetProperty("Animals", out var animalsEl)) continue;

                    foreach (var animalJson in animalsEl.EnumerateArray())
                    {
                        var nickname = animalJson.TryGetProperty("Nickname", out var nNick) ? nNick.GetString() : null;
                        if (nickname is null) continue;
                        var captures = animalJson.TryGetProperty("CapturedCounter", out var ccEl) && ccEl.TryGetInt32(out var ccVal) ? ccVal : 0;

                        if (!perBotMaxCaptures.TryGetValue(nickname, out var existing) || captures > existing)
                        {
                            perBotMaxCaptures[nickname] = captures;
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.Debug(ex, "Failed to parse JSON file {FilePath}", logFile.Path);
                    continue;
                }
            }

            // Get final ranking from last file
            var fileContent = await File.ReadAllTextAsync(finalLogPath, cancellationToken);
            var gameState = JsonSerializer.Deserialize<JsonElement>(fileContent);

            if (!gameState.TryGetProperty("Animals", out var animalsElement))
            {
                return null;
            }

            var animals = animalsElement.EnumerateArray()
                .Select(animal => new
                {
                    Id = animal.TryGetProperty("Id", out var id) ? id.GetString() : null,
                    Nickname = animal.TryGetProperty("Nickname", out var nick) ? nick.GetString() : null,
                    Score = animal.TryGetProperty("Score", out var s) && s.TryGetInt32(out var score) ? score : 0,
                })
                .OrderByDescending(a => a.Score)
                .ToList();

            if (!animals.Any()) return null;

            var result = new Dictionary<string, BotStats>();
            foreach (var (animal, index) in animals.Select((value, i) => (value, i)))
            {
                if (animal.Nickname is null) continue;
                
                var stats = new BotStats
                {
                    Nickname = animal.Nickname,
                    Id = animal.Id,
                    Wins = index == 0 ? 1 : 0,
                    SecondPlaces = index == 1 ? 1 : 0,
                    GamesPlayed = 1,
                    TotalCaptures = perBotMaxCaptures.TryGetValue(animal.Nickname, out var mc) ? mc : 0
                };
                
                result[animal.Nickname] = stats;
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing game run {RunDir}", runDir);
            return null;
        }
    }
}
