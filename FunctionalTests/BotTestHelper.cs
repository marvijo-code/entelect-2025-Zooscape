using System.Reflection;
using System.Text.Json;
using Marvijo.Zooscape.Bots.Common.Abstractions;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Services;
using NSubstitute;
using Serilog;

namespace Marvijo.Zooscape.Bots.FunctionalTests;

public static class BotTestHelper
{
    private static readonly string BasePath = Path.GetDirectoryName(
        Assembly.GetExecutingAssembly().Location
    )!;
    private static readonly string TestStatesPath = Path.Combine(BasePath, "GameStates");

    public static GameState LoadGameState(
        string testFileName,
        string? botIdForState = null,
        Position? startingPosition = null,
        ILogger? logger = null
    )
    {
        logger ??= Substitute.For<ILogger>();
        var filePath = Path.Combine(TestStatesPath, testFileName);
        if (!File.Exists(filePath))
        {
            logger.Error($"Test state file not found: {filePath}");
            throw new FileNotFoundException($"Test state file not found: {filePath}");
        }

        var json = File.ReadAllText(filePath);
        var botStateDto = JsonSerializer.Deserialize<BotStateDTO>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        if (botStateDto == null)
        {
            logger.Error($"Failed to deserialize BotStateDTO from {testFileName}");
            throw new InvalidOperationException(
                $"Failed to deserialize BotStateDTO from {testFileName}"
            );
        }

        if (!string.IsNullOrEmpty(botIdForState) && botStateDto.Animals != null)
        {
            var playerAnimal = botStateDto.Animals.FirstOrDefault(a => a.Id == botIdForState);
            if (playerAnimal != null)
            {
                botStateDto.PlayerId = playerAnimal.Id;
            }
            else
            {
                logger.Warning(
                    $"Specified botIdForState '{botIdForState}' not found in Animals list in {testFileName}."
                );
            }
        }

        var gameState = new GameState(botStateDto, logger);

        if (startingPosition != null && gameState.CurrentPlayer != null)
        {
            gameState.CurrentPlayer.Position = startingPosition;
            logger.Information(
                $"Set starting position for CurrentPlayer {gameState.CurrentPlayer.Id} to {startingPosition}"
            );
        }
        else if (startingPosition != null)
        {
            logger.Warning("StartingPosition provided but CurrentPlayer is null in GameState.");
        }

        return gameState;
    }

    public static List<TestDefinition> LoadTestDefinitions()
    {
        var filePath = Path.Combine(BasePath, "test_definitions.json");
        if (!File.Exists(filePath))
        {
            return new List<TestDefinition>();
        }
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<TestDefinition>>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                }
            ) ?? new List<TestDefinition>();
    }

    public static void SaveTestDefinition(TestDefinition testDefinition)
    {
        var definitions = LoadTestDefinitions();
        definitions.RemoveAll(td =>
            td.Name == testDefinition.Name && td.StateFile == testDefinition.StateFile
        );
        definitions.Add(testDefinition);
        var filePath = Path.Combine(BasePath, "test_definitions.json");
        var json = JsonSerializer.Serialize(
            definitions,
            new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true }
        );
        File.WriteAllText(filePath, json);
    }

    public static string SaveGameStateFile(string testName, string jsonContent)
    {
        var gameStatesDir = Path.Combine(BasePath, "GameStates");
        if (!Directory.Exists(gameStatesDir))
        {
            Directory.CreateDirectory(gameStatesDir);
        }
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeTestName = new string(
            testName.Where(ch => !invalidChars.Contains(ch)).ToArray()
        ).Replace(" ", "_");
        var fileName = $"{safeTestName}_{DateTime.Now:yyyyMMddHHmmssfff}.json";
        var filePath = Path.Combine(gameStatesDir, fileName);
        File.WriteAllText(filePath, jsonContent);
        return fileName;
    }

    public static IActionHistoryService CreateActionHistoryService(ILogger? logger = null)
    {
        return new ActionHistoryService(logger ?? Substitute.For<ILogger>());
    }

    public static IScoreManager CreateScoreManager(ILogger? logger = null)
    {
        logger?.Debug("Creating NSubstitute.For<IScoreManager>() as actual not specified.");
        return Substitute.For<IScoreManager>();
    }

    public static IGameActionPlayer CreateGameActionPlayer(ILogger? logger = null)
    {
        logger?.Debug("Creating NSubstitute.For<IGameActionPlayer>() as actual not specified.");
        return Substitute.For<IGameActionPlayer>();
    }
}
