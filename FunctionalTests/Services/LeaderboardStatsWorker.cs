using FunctionalTests.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FunctionalTests.Services;

/// <summary>
/// Background service that periodically scans the log directory, aggregates per-bot statistics, and
/// stores the result in the <see cref="CacheService"/> so that the public API endpoint is instantaneous.
/// </summary>
public class LeaderboardStatsWorker : BackgroundService
{
    private readonly string _logsDir;
    private readonly CacheService _cache;
    private readonly Serilog.ILogger _logger;

    // How often to refresh the stats. Tweaked to balance freshness and performance.
    private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(30);

    // How long leaderboard results should live in the cache. Keep a bit longer than refresh interval.
    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(2);

    public LeaderboardStatsWorker(IConfiguration config, CacheService cache, Serilog.ILogger logger)
    {
        _logsDir = config["LogsDir"] ?? "C:\\dev\\2025-Zooscape\\logs";
        _cache = cache;
        _logger = logger.ForContext<LeaderboardStatsWorker>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("LeaderboardStatsWorker started. Scanning logs in {LogsDir}", _logsDir);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var stats = await CalculateStatsAsync();
                _cache.Set("leaderboard-stats", stats, _cacheTtl);
                _logger.Information("Leaderboard stats updated. Bots: {Count}", stats.Count);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error computing leaderboard stats");
            }

            try
            {
                await Task.Delay(_refreshInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // shutting down
            }
        }
    }

    private async Task<List<BotStats>> CalculateStatsAsync()
    {
        var botStats = new Dictionary<string, BotStats>();

        if (!Directory.Exists(_logsDir))
        {
            _logger.Warning("Logs directory not found at {LogsDir}", _logsDir);
            return new List<BotStats>();
        }

        var gameRunDirs = Directory.GetDirectories(_logsDir);
        _logger.Debug("Found {RunCount} game run directories", gameRunDirs.Length);

        foreach (var runDir in gameRunDirs)
        {
            try
            {
                var logFiles = Directory.GetFiles(runDir, "*.json")
                                         .Select(f => new { Path = f, Number = int.Parse(Path.GetFileNameWithoutExtension(f)) })
                                         .OrderBy(f => f.Number)
                                         .ToList();
                if (!logFiles.Any()) continue;

                // Compute max captures for each bot within this single game
                var perBotMaxCaptures = new Dictionary<string, int>();
                foreach (var logFile in logFiles)
                {
                    var json = await File.ReadAllTextAsync(logFile.Path);
                    JsonDocument doc;
                    try
                    {
                        doc = JsonDocument.Parse(json);
                    }
                    catch (JsonException)
                    {
                        continue;
                    }
                    if (!doc.RootElement.TryGetProperty("Animals", out var animalsEl)) continue;

                    foreach (var animal in animalsEl.EnumerateArray())
                    {
                        var nickname = animal.TryGetProperty("Nickname", out var nickEl) ? nickEl.GetString() : null;
                        if (nickname is null) continue;
                        var captures = animal.TryGetProperty("CapturedCounter", out var ccEl) && ccEl.TryGetInt32(out var cc) ? cc : 0;
                        if (!perBotMaxCaptures.TryGetValue(nickname, out var existing) || captures > existing)
                        {
                            perBotMaxCaptures[nickname] = captures;
                        }
                    }
                }

                // Use final tick for finishing order (wins / second places)
                var finalLogPath = logFiles.Last().Path;
                var finalJson = await File.ReadAllTextAsync(finalLogPath);
                JsonDocument finalDoc;
                try
                {
                    finalDoc = JsonDocument.Parse(finalJson);
                }
                catch (JsonException)
                {
                    continue;
                }
                if (!finalDoc.RootElement.TryGetProperty("Animals", out var finalAnimalsEl)) continue;

                var animals = finalAnimalsEl.EnumerateArray()
                                            .Select(a => new
                                            {
                                                Id = a.TryGetProperty("Id", out var idEl) ? idEl.GetString() : string.Empty,
                                                Nickname = a.TryGetProperty("Nickname", out var nEl) ? nEl.GetString() : string.Empty,
                                                Score = a.TryGetProperty("Score", out var sEl) && sEl.TryGetInt32(out var sc) ? sc : 0,
                                            })
                                            .OrderByDescending(a => a.Score)
                                            .ToList();

                for (int index = 0; index < animals.Count; index++)
                {
                    var animal = animals[index];
                    if (string.IsNullOrEmpty(animal.Nickname)) continue;
                    if (!botStats.TryGetValue(animal.Nickname, out var stat))
                    {
                        stat = new BotStats
                        {
                            Nickname = animal.Nickname,
                            Id = animal.Id,
                        };
                        botStats[animal.Nickname] = stat;
                    }

                    stat.GamesPlayed++;
                    if (index == 0) stat.Wins++;
                    if (index == 1) stat.SecondPlaces++;
                    var maxCaptures = perBotMaxCaptures.TryGetValue(animal.Nickname, out var max) ? max : 0;
                    stat.TotalCaptures += maxCaptures;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing run directory {RunDir}", runDir);
            }
        }

        // Finalize averages and order list
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

    private static bool TryParseJson(string json, out JsonDocument document)
    {
        try
        {
            document = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            document = null!; // will be ignored by caller when false returned
            return false;
        }
    }
}
