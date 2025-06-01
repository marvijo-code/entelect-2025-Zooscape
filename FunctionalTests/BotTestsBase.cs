using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using NSubstitute;
using Serilog;
using Xunit;

namespace FunctionalTests;

/// <summary>
/// Base class for functional bot testing with complete test infrastructure
/// </summary>
public abstract class BotTestsBase
{
    protected readonly IGameStateLoader _gameStateLoader;
    protected readonly ITestValidator _testValidator;
    protected readonly Serilog.ILogger _logger;

    protected BotTestsBase()
    {
        _logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

        _gameStateLoader = new JsonGameStateLoader(_logger);
        _testValidator = new DefaultTestValidator(_logger);
    }

    /// <summary>
    /// Template method for loading and validating game state
    /// </summary>
    protected virtual void ValidateGameStateLoading(TestParams testParams)
    {
        var gameState = _gameStateLoader.LoadGameState(testParams.TestGameStateJsonPath);
        _testValidator.ValidateGameState(gameState);
    }

    /// <summary>
    /// Template method for testing bot action against game state
    /// </summary>
    protected virtual void TestBotAction<T>(IBot<T> bot, TestParams testParams)
        where T : class
    {
        var gameState = _gameStateLoader.LoadGameState(testParams.TestGameStateJsonPath);

        // Set up bot with test ID
        var testBotId = testParams.BotIdToTest ?? Guid.NewGuid();
        bot.BotId = testBotId;

        // Ensure bot exists in game state
        if (!gameState.Animals.Any(a => a.Id == testBotId))
        {
            _logger.Warning(
                "Bot ID {BotId} not found in game state, using first animal",
                testBotId
            );
            var firstAnimal = gameState.Animals.FirstOrDefault();
            if (firstAnimal != null)
            {
                testBotId = firstAnimal.Id;
                bot.BotId = testBotId;
            }
        }

        var action = bot.GetAction(gameState);

        _testValidator.ValidateBotAction(
            action,
            new List<BotAction> { BotAction.Up, BotAction.Down, BotAction.Left, BotAction.Right },
            testParams.ExpectedAction
        );
        _logger.Information("Bot {BotId} returned action: {Action}", testBotId, action);
    }

    /// <summary>
    /// Test bot by nickname with proper bot lookup
    /// </summary>
    protected virtual void TestBotByNickname<T>(IBot<T> bot, TestParams testParams)
        where T : class
    {
        var gameState = _gameStateLoader.LoadGameState(testParams.TestGameStateJsonPath);

        var animal = gameState.Animals.FirstOrDefault(a =>
            a.Nickname?.Equals(testParams.BotNicknameToTest, StringComparison.OrdinalIgnoreCase)
            == true
        );

        if (animal == null)
        {
            throw new InvalidOperationException(
                $"No animal found with nickname '{testParams.BotNicknameToTest}'"
            );
        }

        bot.BotId = animal.Id;
        var action = bot.GetAction(gameState);

        _testValidator.ValidateBotAction(
            action,
            new List<BotAction> { BotAction.Up, BotAction.Down, BotAction.Left, BotAction.Right },
            testParams.ExpectedAction
        );
        _logger.Information(
            "Bot with nickname '{Nickname}' (ID: {BotId}) returned action: {Action}",
            testParams.BotNicknameToTest,
            animal.Id,
            action
        );
    }

    /// <summary>
    /// Test multiple bots if BotsToTest is specified
    /// </summary>
    protected virtual void TestMultipleBots<T>(IBot<T> bot, TestParams testParams)
        where T : class
    {
        if (testParams.BotsToTest == null || !testParams.BotsToTest.Any())
            return;

        var gameState = _gameStateLoader.LoadGameState(testParams.TestGameStateJsonPath);

        foreach (var botId in testParams.BotsToTest)
        {
            if (!gameState.Animals.Any(a => a.Id == botId))
            {
                _logger.Warning("Bot ID {BotId} not found in game state, skipping", botId);
                continue;
            }

            bot.BotId = botId;
            var action = bot.GetAction(gameState);

            _testValidator.ValidateBotAction(
                action,
                new List<BotAction>
                {
                    BotAction.Up,
                    BotAction.Down,
                    BotAction.Left,
                    BotAction.Right,
                },
                testParams.ExpectedAction
            );
            _logger.Information("Bot {BotId} returned action: {Action}", botId, action);
        }
    }

    /// <summary>
    /// Test tick override functionality
    /// </summary>
    protected virtual void TestTickOverride(TestParams testParams)
    {
        if (!testParams.TickOverride)
            return;

        var gameState = _gameStateLoader.LoadGameState(testParams.TestGameStateJsonPath);
        _testValidator.ValidateTickOverride(gameState);
        _logger.Information(
            "Tick override test passed for game state with tick: {Tick}",
            gameState.Tick
        );
    }

    /// <summary>
    /// Test multiple bot instances from array
    /// </summary>
    protected virtual void TestBotsArray(TestParams testParams)
    {
        if (testParams.BotsArray == null || !testParams.BotsArray.Any())
            return;

        var gameState = _gameStateLoader.LoadGameState(testParams.TestGameStateJsonPath);

        foreach (var bot in testParams.BotsArray)
        {
            _logger.Information("Testing bot instance: {BotType}", bot.GetType().Name);

            // This is a template method - derived classes should override to handle specific bot types
            TestBotFromArray(bot, gameState, testParams);
        }
    }

    /// <summary>
    /// Template method for testing individual bot from array - override in derived classes
    /// </summary>
    protected virtual void TestBotFromArray(object bot, GameState gameState, TestParams testParams)
    {
        _logger.Information(
            "Base TestBotFromArray called for {BotType} - override this method in derived classes",
            bot.GetType().Name
        );
    }
}

/// <summary>
/// Interface for game state loading - following Dependency Inversion Principle
/// </summary>
public interface IGameStateLoader
{
    GameState LoadGameState(string fileName);
}

/// <summary>
/// Interface for test validation - following Single Responsibility Principle
/// </summary>
public interface ITestValidator
{
    void ValidateGameState(GameState gameState);
    void ValidateBotAction(
        BotAction action,
        List<BotAction> acceptableActions,
        BotAction? expectedAction
    );
    void ValidateTickOverride(GameState gameState);
}

/// <summary>
/// Game state loader implementation
/// </summary>
public class JsonGameStateLoader : IGameStateLoader
{
    private readonly Serilog.ILogger _logger;

    public JsonGameStateLoader(Serilog.ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public GameState LoadGameState(string fileName)
    {
        return BotTestHelper.LoadGameState(fileName, _logger);
    }
}

/// <summary>
/// Test validator implementation
/// </summary>
public class DefaultTestValidator : ITestValidator
{
    private readonly Serilog.ILogger _logger;

    public DefaultTestValidator(Serilog.ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void ValidateGameState(GameState gameState)
    {
        // Basic validation
        Assert.NotNull(gameState);
        Assert.NotNull(gameState.Animals);
        Assert.NotNull(gameState.Cells);

        // Enhanced validation
        Assert.True(gameState.Animals.Count > 0, "Game state should have animals");
        Assert.True(gameState.Cells.Count > 0, "Game state should have cells");
        Assert.True(gameState.Tick >= 0, "Game state tick should be non-negative");

        _logger.Information("Game state validation passed");
    }

    public void ValidateBotAction(
        BotAction action,
        List<BotAction> acceptableActions,
        BotAction? expectedAction
    )
    {
        Assert.Contains(action, acceptableActions);
        if (expectedAction.HasValue)
        {
            Assert.Equal(expectedAction.Value, action);
            _logger.Information(
                "Bot action {Action} matches expected {ExpectedAction}",
                action,
                expectedAction.Value
            );
        }
    }

    public void ValidateTickOverride(GameState gameState)
    {
        Assert.True(gameState.Tick >= 0, "Game state tick should be non-negative");
        _logger.Information(
            "Tick override test passed for game state with tick: {Tick}",
            gameState.Tick
        );
    }
}
