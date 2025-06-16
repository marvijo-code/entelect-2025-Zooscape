using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace FunctionalTests.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReplayController : ControllerBase
{
    private readonly string _logsDir;
    private readonly Serilog.ILogger _logger;
    private readonly Services.CacheService _cache;

    public ReplayController(IConfiguration configuration, Serilog.ILogger logger, Services.CacheService cache)
    {
        _logsDir = configuration["LogsDir"] ?? "C:\\dev\\2025-Zooscape\\logs";
        _logger = logger.ForContext<ReplayController>();
        _cache = cache;
    }

    [HttpGet("games")]
    public async Task<IActionResult> GetGames()
    {
        try
        {
            _logger.Information("API: Fetching games list");
            if (!Directory.Exists(_logsDir))
            {
                _logger.Warning("Logs directory not found at {LogsDir}", _logsDir);
                return Ok(new { games = new List<object>() });
            }

            var games = await _cache.GetOrCreateAsync("games-list", async () =>
            {
                
                var gameRunDirs = Directory.GetDirectories(_logsDir);
                var gameTasks = gameRunDirs.Select(async runDir =>
                {
                    try
                    {
                        var files = Directory.GetFiles(runDir, "*.json");
                        if (files.Length == 0) return null;

                        var firstLogPath = files.Select(f => new { Path = f, Number = int.Parse(Path.GetFileNameWithoutExtension(f)) })
                                                .OrderBy(f => f.Number).First().Path;
                        var fileContent = await System.IO.File.ReadAllTextAsync(firstLogPath);
                        var gameData = JsonNode.Parse(fileContent);
                        var runId = new DirectoryInfo(runDir).Name;

                        return new
                        {
                            id = runId,
                            name = $"Game {runId}",
                            date = new FileInfo(runDir).LastWriteTimeUtc,
                            playerCount = (gameData["animals"] ?? gameData["Animals"])?.AsArray().Count ?? 0,
                            tickCount = files.Length
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error processing run {RunDir}", runDir);
                        return null;
                    }
                });

                var results = await Task.WhenAll(gameTasks);
                return results.Where(r => r != null).OrderByDescending(g => g!.date).ToList();
            }, TimeSpan.FromMinutes(5));

            return Ok(new { games });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting games list");
            return StatusCode(500, "Failed to get games list");
        }
    }

    [HttpGet("games/{gameId}")]
    public async Task<IActionResult> GetGameData(string gameId)
    {
        var gameDir = Path.Combine(_logsDir, gameId);
        if (!Directory.Exists(gameDir)) return NotFound(new { error = "Game not found" });

        var worldStates = await _cache.GetOrCreateAsync($"game-{gameId}", async () =>
        {
            
            var logFiles = Directory.GetFiles(gameDir, "*.json")
                                .Select(f => new { Path = f, Number = int.Parse(Path.GetFileNameWithoutExtension(f)) })
                                .OrderBy(f => f.Number)
                                .Select(f => f.Path)
                                .ToList();

            var states = new List<JsonNode>();
            foreach (var logFile in logFiles)
            {
                var content = await System.IO.File.ReadAllTextAsync(logFile);
                var node = JsonNode.Parse(content);
                if (node == null)
                {
                    _logger.Warning("Failed to parse JSON content from log file: {LogFile}. Skipping file.", logFile);
                    continue;
                }
                // Basic normalization for compatibility
                var obj = node.AsObject();
                if (obj.TryGetPropertyValue("Animals", out var animalsNode) && animalsNode != null && !obj.ContainsKey("animals")) obj["animals"] = animalsNode.DeepClone();
                if (obj.TryGetPropertyValue("Cells", out var cellsNode) && cellsNode != null && !obj.ContainsKey("cells")) obj["cells"] = cellsNode.DeepClone();
                states.Add(node);
            }
            return states;
        }, TimeSpan.FromMinutes(10));

        return Ok(new { gameId, worldStates = worldStates ?? new List<System.Text.Json.Nodes.JsonNode>() });
    }

    [HttpGet("{gameId}/{tick}")]
    public async Task<IActionResult> GetReplayTick(string gameId, int tick)
    {
        var logFile = Path.Combine(_logsDir, gameId, $"{tick}.json");
        if (!System.IO.File.Exists(logFile)) return NotFound(new { error = "Game state for the specified tick not found" });

        var gameState = await _cache.GetOrCreateAsync($"replay-{gameId}-{tick}", async () =>
        {
            
            var content = await System.IO.File.ReadAllTextAsync(logFile);
            return content;
        }, TimeSpan.FromMinutes(1));

        return Content(gameState, "application/json");
    }

    [HttpGet("file/load-json")]
    public async Task<IActionResult> LoadJsonFromPath([FromQuery] string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return BadRequest(new { error = "File path query parameter is required." });
        }

        try
        {
            // Security Checks
            if (!Path.IsPathRooted(path))
            {
                _logger.Warning("Access denied: Path is not absolute: {Path}", path);
                return BadRequest(new { error = "Invalid path: Must be an absolute file path." });
            }

            var resolvedPath = Path.GetFullPath(path);
            var allowedBaseDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            if (!resolvedPath.StartsWith(allowedBaseDir, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Warning("Access denied: Path is outside allowed base directory. Resolved: {ResolvedPath}, Base: {AllowedBaseDir}", resolvedPath, allowedBaseDir);
                return StatusCode(403, new { error = "Access denied: Path is outside allowed directory." });
            }

            var ext = Path.GetExtension(resolvedPath).ToLowerInvariant();
            if (ext != ".json" && ext != ".log")
            {
                _logger.Warning("Access denied: Invalid file extension: {Extension} for path {ResolvedPath}", ext, resolvedPath);
                return BadRequest(new { error = "Invalid file type. Only .json and .log files are allowed." });
            }

            var fileContent = await System.IO.File.ReadAllTextAsync(resolvedPath);
            var jsonData = System.Text.Json.Nodes.JsonNode.Parse(fileContent);
            _logger.Information("Successfully loaded and parsed JSON from {ResolvedPath}", resolvedPath);
            return Ok(jsonData);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { error = "File not found." });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { error = "Access to file denied." });
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "Invalid JSON content in file." });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing path {Path}", path);
            return StatusCode(500, new { error = "Failed to load JSON from file." });
        }
    }
}
