using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Enums;
using Newtonsoft.Json;

namespace FindPelletIgnoreStates;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: dotnet run -- <path_to_log_directory>");
            return;
        }

        string logDirectory = args[0];
        string botNickname = "StaticHeuro";

        if (!Directory.Exists(logDirectory))
        {
            Console.WriteLine($"Error: Directory not found at '{logDirectory}'");
            return;
        }

        var jsonFiles = Directory.GetFiles(logDirectory, "*.json")
                                 .Select(f => new { Path = f, Tick = int.Parse(Path.GetFileNameWithoutExtension(f)) })
                                 .OrderBy(f => f.Tick)
                                 .ToList();

        Console.WriteLine($"Found {jsonFiles.Count} log files. Analyzing for bot '{botNickname}'...");

        for (int i = 0; i < jsonFiles.Count - 1; i++)
        {
            var currentFile = jsonFiles[i];
            var nextFile = jsonFiles[i + 1];

            if (currentFile.Tick + 1 != nextFile.Tick)
            {
                continue; // Skip non-consecutive ticks
            }

            try
            {
                var currentGameState = JsonConvert.DeserializeObject<GameState>(File.ReadAllText(currentFile.Path));
                var nextGameState = JsonConvert.DeserializeObject<GameState>(File.ReadAllText(nextFile.Path));

                var bot = currentGameState.Animals.FirstOrDefault(a => a.Nickname == botNickname);
                if (bot == null)
                {
                    continue;
                }

                var (ignored, direction) = CheckForIgnoredPellet(currentGameState, nextGameState, bot);
                if (ignored)
                {
                    Console.WriteLine($"Potential issue found at tick {currentFile.Tick}: Bot ignored pellet in direction {direction}.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing tick {currentFile.Tick}: {ex.Message}");
            }
        }
    }

    static (bool, BotAction?) CheckForIgnoredPellet(GameState currentState, GameState nextState, Animal bot)
    {
        var legalMoves = GetLegalMoves(currentState, bot);

        foreach (var move in legalMoves)
        {
            int nx = bot.X, ny = bot.Y;
            GetNextPosition(move, ref nx, ref ny);

            var targetCell = currentState.Cells.FirstOrDefault(c => c.X == nx && c.Y == ny);

            if (targetCell?.Content == CellContent.Pellet)
            {
                var nextBotPosition = nextState.Animals.FirstOrDefault(a => a.Id == bot.Id);
                if (nextBotPosition != null && (nextBotPosition.X != nx || nextBotPosition.Y != ny))
                {
                    return (true, move); // Ignored a pellet in this direction
                }
            }
        }

        return (false, null);
    }

    static List<BotAction> GetLegalMoves(GameState state, Animal bot)
    {
        var legalActions = new List<BotAction>();
        var allPossibleActions = new[] { BotAction.Up, BotAction.Down, BotAction.Left, BotAction.Right };

        foreach (var action in allPossibleActions)
        {
            int nx = bot.X, ny = bot.Y;
            GetNextPosition(action, ref nx, ref ny);

            var targetCell = state.Cells.FirstOrDefault(c => c.X == nx && c.Y == ny);
            if (targetCell != null && targetCell.Content != CellContent.Wall)
            {
                legalActions.Add(action);
            }
        }
        return legalActions;
    }

    static void GetNextPosition(BotAction action, ref int x, ref int y)
    {
        switch (action)
        {
            case BotAction.Up: y--; break;
            case BotAction.Down: y++; break;
            case BotAction.Left: x--; break;
            case BotAction.Right: x++; break;
        }
    }
}
