using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using Newtonsoft.Json;

namespace CaptureAnalysis;

/// <summary>
/// Scans a directory of sequential GameState log files (one JSON file per tick) and
/// detects capture events for the specified bot. For each capture it attempts a
/// simple avoidability analysis: were there any legal moves on the tick *before*
/// the capture that would *not* place the bot onto a zookeeper and would stay
/// at distance > 0 from all zookeepers.
///
/// USAGE:
///     dotnet run -- <path_to_log_directory> [BotNickname]
///
/// Example:
///     dotnet run -- ../../logs/2025-07-15 StaticHeuro
/// </summary>
internal static class Program
{
    private static readonly BotAction[] AllMoves =
    {
        BotAction.Up,
        BotAction.Down,
        BotAction.Left,
        BotAction.Right
    };

    private static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: dotnet run -- <path_to_log_directory> [BotNickname] ");
            return;
        }

        var logDir = args[0];
        var botNickname = args.Length > 1 ? args[1] : "StaticHeuro";

        if (!Directory.Exists(logDir))
        {
            Console.WriteLine($"Error: Directory '{logDir}' does not exist.");
            return;
        }

        var jsonFiles = Directory.GetFiles(logDir, "*.json")
                                 .Select(f => new { Path = f, Tick = ParseTick(Path.GetFileNameWithoutExtension(f)) })
                                 .Where(x => x.Tick.HasValue)
                                 .OrderBy(x => x.Tick!.Value)
                                 .ToList();

        if (jsonFiles.Count < 2)
        {
            Console.WriteLine("Need at least 2 consecutive tick files to analyze captures.");
            return;
        }

        Console.WriteLine($"Analyzing {jsonFiles.Count} game state files for bot '{botNickname}'\n");

        for (var i = 0; i < jsonFiles.Count - 1; i++)
        {
            var currInfo = jsonFiles[i];
            var nextInfo = jsonFiles[i + 1];

            // Only consider consecutive ticks
            if (nextInfo.Tick!.Value != currInfo.Tick!.Value + 1) continue;

            var currentState = ReadState(currInfo.Path);
            var nextState = ReadState(nextInfo.Path);
            if (currentState == null || nextState == null) continue;

            var botPrev = currentState.Animals.FirstOrDefault(a => a.Nickname == botNickname);
            var botNext = nextState.Animals.FirstOrDefault(a => a.Nickname == botNickname);
            if (botPrev == null || botNext == null) continue;

            if (botNext.CapturedCounter > botPrev.CapturedCounter)
            {
                var avoidable = WasCaptureAvoidable(currentState, botPrev);
                var verdict = avoidable ? "AVOIDABLE" : "UNAVOIDABLE";
                Console.WriteLine($"Capture detected at tick {nextInfo.Tick}: {verdict}");
            }
        }
    }

    private static bool WasCaptureAvoidable(GameState stateBefore, Animal bot)
    {
        var legalMoves = GetLegalMoves(stateBefore, bot);
        if (legalMoves.Count == 0) return false; // No moves, cannot avoid

        var zookeeperPositions = stateBefore.Zookeepers.Select(z => (z.X, z.Y)).ToHashSet();

        foreach (var move in legalMoves)
        {
            var (nx, ny) = BotUtils.ApplyMove(bot.X, bot.Y, move);

            // Skip if that exact cell has a zookeeper already
            if (zookeeperPositions.Contains((nx, ny))) continue;

            // Also skip if zookeeper is adjacent (Manhattan distance 1) -> risky
            var safe = stateBefore.Zookeepers.All(z => BotUtils.ManhattanDistance(z.X, z.Y, nx, ny) > 1);
            if (safe) return true; // At least one safe move exists
        }

        return false;
    }

    private static List<BotAction> GetLegalMoves(GameState state, Animal bot)
    {
        var result = new List<BotAction>();
        foreach (var move in AllMoves)
        {
            var (nx, ny) = BotUtils.ApplyMove(bot.X, bot.Y, move);
            if (BotUtils.IsTraversable(state, nx, ny)) result.Add(move);
        }
        return result;
    }

    private static int? ParseTick(string? fileNameNoExt)
    {
        return int.TryParse(fileNameNoExt, out var t) ? t : null;
    }

    private static GameState? ReadState(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<GameState>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to read {path}: {ex.Message}");
            return null;
        }
    }
}
