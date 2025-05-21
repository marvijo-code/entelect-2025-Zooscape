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
        _logger.Information($"Running test: {testParams.TestName} for bot: {bot.GetType().Name}");

        GameState gameState;
        try
        {
            gameState = BotTestHelper.LoadGameState(
                testParams.TestFileName,
                testParams.BotIdToTest ?? bot.BotId.ToString(),
                testParams.StartingPosition
            );
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                $"Failed to load game state for test: {testParams.TestName}, file: {testParams.TestFileName}"
            );
            Assert.Fail(
                $"Failed to load game state for test: {testParams.TestName}, file: {testParams.TestFileName}. Error: {ex.Message}"
            );
            return; // Keep analyzer happy
        }

        if (testParams.Tick.HasValue)
        {
            gameState.Tick = testParams.Tick.Value;
        }

        //// Ensure bot ID is correctly set for the game state, especially if BotIdToTest was used in LoadGameState
        //// and the bot instance itself might have a different default ID.
        //// This assumes the bot.BotId can be set or is already correctly initialized.
        //if (gameState.CurrentPlayer != null) // CurrentPlayer should be the bot being tested
        //{
        //    bot.BotId = Guid.Parse(gameState.CurrentPlayer.Id);
        //     _logger.Information($"Ensuring bot {bot.GetType().Name} has ID {bot.BotId} for test {testParams.TestName}");
        //}
        //else
        //{
        //    _logger.Warning($"CurrentPlayer is null in GameState for test {testParams.TestName}. Bot ID might not be set correctly for the bot instance.");
        //    // If BotIdToTest is provided, we should ensure the bot instance uses it.
        //    if (!string.IsNullOrEmpty(testParams.BotIdToTest) && Guid.TryParse(testParams.BotIdToTest, out var parsedBotId))
        //    {
        //        bot.BotId = parsedBotId;
        //        _logger.Information($"Setting bot {bot.GetType().Name} ID to {bot.BotId} from TestParams for test {testParams.TestName}");
        //    }
        //}


        BotAction chosenAction;
        try
        {
            chosenAction = bot.GetAction(gameState);
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                $"Bot {bot.GetType().Name} threw an exception during GetAction for test: {testParams.TestName}"
            );
            Assert.Fail(
                $"Bot {bot.GetType().Name} threw an exception for test: {testParams.TestName}. Error: {ex.Message}"
            );
            return; // Keep analyzer happy
        }

        _logger.Information(
            $"Bot {bot.GetType().Name} chose action: {chosenAction} for test: {testParams.TestName}"
        );

        if (testParams.ExpectedAction.HasValue)
        {
            // If an ExpectedAction is specified, it must be one of the AcceptableActions
            if (!testParams.AcceptableActions.Contains(testParams.ExpectedAction.Value))
            {
                testParams.AcceptableActions.Add(testParams.ExpectedAction.Value);
                _logger.Warning(
                    $"ExpectedAction {testParams.ExpectedAction.Value} was not in AcceptableActions for test {testParams.TestName}. It has been added."
                );
            }
            Assert.True(
                testParams.AcceptableActions.Contains(chosenAction),
                $"Test: {testParams.TestName}, Bot: {bot.GetType().Name}. Expected one of [{string.Join(", ", testParams.AcceptableActions)}] but got {chosenAction}. Preferred was {testParams.ExpectedAction.Value}"
            );
        }
        else
        {
            Assert.True(
                testParams.AcceptableActions.Contains(chosenAction),
                $"Test: {testParams.TestName}, Bot: {bot.GetType().Name}. Expected one of [{string.Join(", ", testParams.AcceptableActions)}] but got {chosenAction}."
            );
        }
    }
}
