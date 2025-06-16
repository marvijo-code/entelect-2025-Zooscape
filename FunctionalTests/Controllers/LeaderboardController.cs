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
        _logger.Information("API: Calculating leaderboard stats from logs {LogsDir}", _logsDir);

        if (!Directory.Exists(_logsDir))
        {
            _logger.Warning("Logs directory not found at {LogsDir}", _logsDir);
            return Ok(new List<BotStats>());
        }


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
                            Score = animal.TryGetProperty("Score", out var s) && s.TryGetInt32(out var score) ? score : 0
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
                                GamesPlayed = 0
                            };
                            botStats[animal.Nickname] = stats;
                        }

                        stats.GamesPlayed++;
                        if (index == 0) stats.Wins++;
                        if (index == 1) stats.SecondPlaces++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error processing game run {RunDir}", runDir);
                }
            }

            return botStats.Values
                            .OrderByDescending(b => b.Wins)
                            .ThenByDescending(b => b.SecondPlaces)
                            .ThenByDescending(b => b.GamesPlayed)
                            .ToList();
        }, TimeSpan.FromSeconds(5));

        _logger.Information("Returning leaderboard with {BotCount} bots", leaderboard?.Count ?? 0);
        return Ok(leaderboard);
    }
}
