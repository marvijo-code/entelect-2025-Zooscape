using System.Reflection;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Abstractions;
using Marvijo.Zooscape.Bots.Common.Models;
using NSubstitute;
using Serilog;

// Add using statements for each of your bot namespaces here, e.g.:
// using Bots.RulesBot;
// using Bots.GatherNearBot;

namespace Marvijo.Zooscape.Bots.FunctionalTests;

public static class BotFactory
{
    public static IEnumerable<IBot<GameState>> GetAllBots(ILogger? logger = null)
    {
        logger ??= Substitute.For<ILogger>();
        var bots = new List<IBot<GameState>>();
        var botInterfaceType = typeof(IBot<GameState>);

        // Determine the directory of the current assembly (FunctionalTests)
        var currentAssemblyLocation = Assembly.GetExecutingAssembly().Location;
        var functionalTestsDir = Path.GetDirectoryName(currentAssemblyLocation);
        if (string.IsNullOrEmpty(functionalTestsDir))
        {
            logger.Error("Could not determine the directory of the FunctionalTests assembly.");
            return bots; // Return empty list
        }

        // Navigate to the solution root (assuming Bots folder is at ../Bots relative to FunctionalTests project output dir)
        // This might need adjustment based on your actual output structure.
        // A common structure: <SolutionRoot>/FunctionalTests/bin/Debug/netX.X/ and <SolutionRoot>/Bots/
        var solutionRootGuess = Path.GetFullPath(
            Path.Combine(functionalTestsDir, "..", "..", "..")
        ); // Adjust as needed
        var botsBaseDirectory = Path.Combine(solutionRootGuess, "Bots");

        logger.Information($"Attempting to load bots from base directory: {botsBaseDirectory}");

        if (!Directory.Exists(botsBaseDirectory))
        {
            logger.Warning(
                $"Bots base directory not found: {botsBaseDirectory}. Searched from {functionalTestsDir}. Cannot dynamically load bots."
            );
            return bots; // Return empty list if the main Bots folder doesn't exist
        }

        // Loop through each subdirectory in the Bots folder (e.g., Bots/ClingyHeuroBot, Bots/RulesBot)
        foreach (var botProjectDir in Directory.GetDirectories(botsBaseDirectory))
        {
            var botProjectName = Path.GetFileName(botProjectDir);
            // Convention: Assembly name matches the project folder name (e.g., ClingyHeuroBot.dll)
            var assemblyPath = Path.Combine(
                botProjectDir,
                "bin",
                "Debug",
                "net8.0",
                $"{botProjectName}.dll"
            ); // Adjust TFM (net8.0) if necessary
            // Fallback for Release configuration
            if (!File.Exists(assemblyPath))
            {
                assemblyPath = Path.Combine(
                    botProjectDir,
                    "bin",
                    "Release",
                    "net8.0",
                    $"{botProjectName}.dll"
                );
            }

            if (File.Exists(assemblyPath))
            {
                try
                {
                    logger.Information($"Loading assembly: {assemblyPath}");
                    var assembly = Assembly.LoadFrom(assemblyPath);
                    var botTypes = assembly
                        .GetTypes()
                        .Where(t =>
                            botInterfaceType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract
                        );

                    foreach (var botType in botTypes)
                    {
                        try
                        {
                            logger.Information(
                                $"Attempting to create instance of bot: {botType.FullName}"
                            );
                            // Prepare dependencies for the bot constructor
                            // This assumes a common constructor: (IActionHistoryService, IScoreManager, IGameActionPlayer, ILogger)
                            var historyService = BotTestHelper.CreateActionHistoryService(logger);
                            var scoreManager = BotTestHelper.CreateScoreManager(logger); // Might be a mock if not implemented
                            var gameActionPlayer = BotTestHelper.CreateGameActionPlayer(logger); // Might be a mock

                            var constructor = botType.GetConstructor(
                                new[]
                                {
                                    typeof(IActionHistoryService),
                                    typeof(IScoreManager),
                                    typeof(IGameActionPlayer),
                                    typeof(ILogger),
                                }
                            );
                            if (constructor != null)
                            {
                                var botInstance = (IBot<GameState>?)
                                    Activator.CreateInstance(
                                        botType,
                                        historyService,
                                        scoreManager,
                                        gameActionPlayer,
                                        logger
                                    );
                                if (botInstance != null)
                                {
                                    bots.Add(botInstance);
                                    logger.Information(
                                        $"Successfully instantiated and added bot: {botType.FullName}"
                                    );
                                }
                            }
                            else
                            {
                                // Attempt parameterless constructor as a fallback, if your bots might have it
                                var parameterlessConstructor = botType.GetConstructor(
                                    Type.EmptyTypes
                                );
                                if (parameterlessConstructor != null)
                                {
                                    var botInstance = (IBot<GameState>?)
                                        Activator.CreateInstance(botType);
                                    if (botInstance != null)
                                    {
                                        // Manually set dependencies if properties are available and it makes sense
                                        // e.g., if (botInstance is BaseBot baseBot) { baseBot.Logger = logger; ... }
                                        bots.Add(botInstance);
                                        logger.Information(
                                            $"Successfully instantiated {botType.FullName} using parameterless constructor."
                                        );
                                    }
                                }
                                else
                                {
                                    logger.Warning(
                                        $"No suitable constructor found for bot type {botType.FullName} in assembly {assemblyPath}. Expected (IActionHistoryService, IScoreManager, IGameActionPlayer, ILogger) or parameterless."
                                    );
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error(
                                ex,
                                $"Error instantiating bot type {botType.FullName} from assembly {assemblyPath}: {ex.Message}"
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Error loading assembly {assemblyPath}: {ex.Message}");
                }
            }
            else
            {
                logger.Warning(
                    $"Assembly not found for bot project {botProjectName} at expected path: {assemblyPath}"
                );
            }
        }

        if (!bots.Any())
        {
            logger.Warning(
                "No bots were loaded by BotFactory. Check paths, TFM, and ensure bot projects are built and have correct constructors."
            );
        }

        return bots;
    }
}
