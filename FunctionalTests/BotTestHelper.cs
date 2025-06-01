using System;
using System.IO;
using System.Reflection;
using Marvijo.Zooscape.Bots.Common.Models;
using Newtonsoft.Json;
using Serilog;

namespace FunctionalTests;

/// <summary>
/// Helper class for loading game states from JSON files
/// </summary>
public static class BotTestHelper
{
    /// <summary>
    /// Load a game state from a JSON file
    /// </summary>
    /// <param name="fileName">The JSON file name (relative to GameStates directory)</param>
    /// <param name="logger">Logger for debugging</param>
    /// <returns>Loaded GameState object</returns>
    public static GameState LoadGameState(string fileName, Serilog.ILogger logger)
    {
        try
        {
            // Get the directory where the test assembly is located
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);

            // Construct the full path to the game state file in GameStates subdirectory
            var gameStatesPath = Path.Combine(assemblyDirectory!, "GameStates", fileName);

            logger.Information("Loading game state from: {FilePath}", gameStatesPath);

            if (!File.Exists(gameStatesPath))
            {
                throw new FileNotFoundException($"Game state file not found: {gameStatesPath}");
            }

            var jsonContent = File.ReadAllText(gameStatesPath);
            logger.Information("JSON content length: {Length} characters", jsonContent.Length);

            var gameState = JsonConvert.DeserializeObject<GameState>(jsonContent);

            if (gameState == null)
            {
                throw new InvalidOperationException("Failed to deserialize game state from JSON");
            }

            logger.Information(
                "Successfully loaded game state with {AnimalCount} animals and {CellCount} cells",
                gameState.Animals?.Count ?? 0,
                gameState.Cells?.Count ?? 0
            );

            return gameState;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error loading game state from {FileName}", fileName);
            throw;
        }
    }
}
