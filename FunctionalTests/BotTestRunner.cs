using FunctionalTests;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using NSubstitute;
using Serilog;
using Xunit;

namespace Marvijo.Zooscape.Bots.FunctionalTests;

public class BotTestRunner
{
    private readonly ILogger _logger;

    public BotTestRunner(ILogger? logger = null)
    {
        _logger = logger ?? Substitute.For<ILogger>();
    }

    public void RunTest(IBot<GameState> bot, FunctionalTestParams testParams)
    {
        _logger.Information(
            $"BOT_TEST_RUNNER: Running test '{testParams.TestName}' for bot type '{bot.GetType().Name}'. Game state file: '{testParams.TestFileName}'. BotId for state loading: '{testParams.BotIdToTest ?? "(not specified, will infer from state)"}'."
        );

        GameState gameState;
        try
        {
            // testParams.BotIdToTest is the ID of the player whose turn it is in the GameStateFile.
            // This helps LoadGameState to correctly identify the CurrentPlayer if the JSON contains multiple animals.
            gameState = BotTestHelper.LoadGameState(
                testParams.TestFileName,
                testParams.BotIdToTest,
                testParams.StartingPosition,
                _logger
            );
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                $"BOT_TEST_RUNNER: Failed to load game state for test: {testParams.TestName}, file: {testParams.TestFileName}. Error: {ex.Message}"
            );
            Assert.Fail(
                $"BOT_TEST_RUNNER: Failed to load game state for test: {testParams.TestName}, file: {testParams.TestFileName}. Error: {ex.Message}"
            );
            return;
        }

        if (testParams.Tick.HasValue)
        {
            gameState.Tick = testParams.Tick.Value;
            _logger.Information(
                $"BOT_TEST_RUNNER: Test '{testParams.TestName}', Bot '{bot.GetType().Name}': Set GameState.Tick to {gameState.Tick}."
            );
        }

        // Critical step: Align the bot instance's ID with the CurrentPlayer in the loaded GameState.
        // The bot.GetAction(gameState) will likely use bot.BotId to find itself within gameState.Animals or gameState.CurrentPlayer.
        if (gameState.CurrentPlayer != null)
        {
            if (Guid.TryParse(gameState.CurrentPlayer.Id, out var currentPlayerIdFromState))
            {
                bot.BotId = currentPlayerIdFromState;
                _logger.Information(
                    $"BOT_TEST_RUNNER: Test '{testParams.TestName}', Bot '{bot.GetType().Name}': Aligned bot instance ID to GameState.CurrentPlayer.Id: {bot.BotId}."
                );
            }
            else
            {
                _logger.Warning(
                    $"BOT_TEST_RUNNER: Test '{testParams.TestName}', Bot '{bot.GetType().Name}': GameState.CurrentPlayer.Id '{gameState.CurrentPlayer.Id}' is not a valid Guid. Cannot align bot instance ID."
                );
                Assert.Fail(
                    $"GameState.CurrentPlayer.Id '{gameState.CurrentPlayer.Id}' is not a valid Guid for test '{testParams.TestName}'."
                );
                return;
            }
        }
        else
        {
            _logger.Error(
                $"BOT_TEST_RUNNER: Test '{testParams.TestName}', Bot '{bot.GetType().Name}': GameState.CurrentPlayer is null after loading state '{testParams.TestFileName}'. Cannot run test meaningfully as bot ID cannot be aligned."
            );
            Assert.Fail(
                $"GameState.CurrentPlayer is null for test '{testParams.TestName}' using file '{testParams.TestFileName}'. Check GameState loading and BotIdToTest ('{testParams.BotIdToTest}')."
            );
            return;
        }

        BotAction chosenAction;
        try
        {
            _logger.Information(
                $"BOT_TEST_RUNNER: Test '{testParams.TestName}', Bot '{bot.GetType().Name} (ID: {bot.BotId})': Calling GetAction()."
            );
            chosenAction = bot.GetAction(gameState);
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                $"BOT_TEST_RUNNER: Bot '{bot.GetType().Name}' (ID: {bot.BotId}) threw an exception during GetAction() for test: {testParams.TestName}. Error: {ex.Message}"
            );
            Assert.Fail(
                $"Bot '{bot.GetType().Name}' (ID: {bot.BotId}) threw an exception for test: {testParams.TestName}. Error: {ex.Message}"
            );
            return;
        }

        _logger.Information(
            $"BOT_TEST_RUNNER: Test '{testParams.TestName}', Bot '{bot.GetType().Name}' (ID: {bot.BotId}) chose action: {chosenAction}."
        );

        var effectiveAcceptableActions = new List<BotAction>(testParams.AcceptableActions);
        if (
            testParams.ExpectedAction.HasValue
            && !effectiveAcceptableActions.Contains(testParams.ExpectedAction.Value)
        )
        {
            _logger.Warning(
                $"BOT_TEST_RUNNER: Test '{testParams.TestName}': ExpectedAction '{testParams.ExpectedAction.Value}' was not in AcceptableActions. It has been added for this assertion."
            );
            effectiveAcceptableActions.Add(testParams.ExpectedAction.Value);
        }

        if (!effectiveAcceptableActions.Any())
        {
            _logger.Error(
                $"BOT_TEST_RUNNER: Test '{testParams.TestName}', Bot '{bot.GetType().Name}': No acceptable actions defined or parsed. Cannot assert."
            );
            Assert.Fail($"No acceptable actions defined for test '{testParams.TestName}'.");
            return;
        }

        Assert.True(
            effectiveAcceptableActions.Contains(chosenAction),
            $"Test: '{testParams.TestName}', Bot: '{bot.GetType().Name}' (ID: {bot.BotId}). Expected one of [{string.Join(", ", effectiveAcceptableActions)}] but got '{chosenAction}'. {(testParams.ExpectedAction.HasValue ? "Preferred was " + testParams.ExpectedAction.Value : "")}"
        );

        _logger.Information(
            $"BOT_TEST_RUNNER: Test '{testParams.TestName}' for bot '{bot.GetType().Name}' (ID: {bot.BotId}) PASSED. Action '{chosenAction}' was acceptable."
        );
    }
}
