using System;
using System.Collections.Generic;
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Xunit;
using ClingyHeuroBot2Service = HeuroBot.Services.HeuroBotService;
using ClingyHeuroBotService = HeuroBotV2.Services.HeuroBotService;

namespace FunctionalTests;

/// <summary>
/// Standard functional tests for bot behavior validation
/// </summary>
public class StandardBotTests : BotTestsBase
{
    private readonly ClingyHeuroBot2Service _clingyHeuroBot2;
    private readonly ClingyHeuroBotService _clingyHeuroBot;

    public StandardBotTests()
    {
        _clingyHeuroBot2 = new ClingyHeuroBot2Service();
        _clingyHeuroBot = new ClingyHeuroBotService();
    }

    protected override void TestBotFromArray(object bot, GameState gameState, TestParams testParams)
    {
        _logger.Information("Testing bot from array: {BotType}", bot.GetType().Name);

        if (bot is ClingyHeuroBot2Service clingyBot2)
        {
            TestBotGeneric(clingyBot2, gameState, testParams);
        }
        else if (bot is ClingyHeuroBotService clingyBot)
        {
            TestBotGeneric(clingyBot, gameState, testParams);
        }
        else
        {
            _logger.Warning("Unknown bot type: {BotType}", bot.GetType().Name);
        }
    }

    private void TestBotGeneric<T>(IBot<T> bot, GameState gameState, TestParams testParams)
        where T : class
    {
        // Set up bot with test ID
        var testBotId = testParams.BotIdToTest ?? Guid.NewGuid();

        // If testing by nickname, find the animal with that nickname
        if (!string.IsNullOrEmpty(testParams.BotNicknameToTest))
        {
            var animal = gameState.Animals.FirstOrDefault(a =>
                a.Nickname?.Equals(testParams.BotNicknameToTest, StringComparison.OrdinalIgnoreCase)
                == true
            );

            if (animal != null)
            {
                testBotId = animal.Id;
            }
            else
            {
                _logger.Warning(
                    "No animal found with nickname '{Nickname}', using first animal",
                    testParams.BotNicknameToTest
                );
                var firstAnimal = gameState.Animals.FirstOrDefault();
                if (firstAnimal != null)
                {
                    testBotId = firstAnimal.Id;
                }
            }
        }
        else if (!gameState.Animals.Any(a => a.Id == testBotId))
        {
            _logger.Warning(
                "Bot ID {BotId} not found in game state, using first animal",
                testBotId
            );
            var firstAnimal = gameState.Animals.FirstOrDefault();
            if (firstAnimal != null)
            {
                testBotId = firstAnimal.Id;
            }
        }

        bot.BotId = testBotId;
        var action = bot.GetAction(gameState);

        // If AcceptableActions is empty but ExpectedAction is set, add ExpectedAction to AcceptableActions
        var acceptableActions = testParams.AcceptableActions;
        if (!acceptableActions.Any() && testParams.ExpectedAction.HasValue)
        {
            acceptableActions = [testParams.ExpectedAction.Value];
        }

        _testValidator.ValidateBotAction(action, acceptableActions, testParams.ExpectedAction);
        _logger.Information(
            "Bot {BotType} with ID {BotId} returned action: {Action}",
            bot.GetType().Name,
            testBotId,
            action
        );
    }

    [Fact]
    public void GameState34LoadTest()
    {
        var testParams = new TestParams
        {
            TestName = "GameState34LoadTest",
            TestGameStateJsonPath = "34.json",
            AcceptableActions = new List<BotAction>
            {
                BotAction.Up,
                BotAction.Down,
                BotAction.Left,
                BotAction.Right,
            },
            TestDescription = "Validate game state 34 loads correctly",
        };

        ValidateGameStateLoading(testParams);
    }

    [Fact]
    public void GameState34ActionTest()
    {
        var testParams = new TestParams
        {
            TestName = "GameState34ActionTest",
            TestGameStateJsonPath = "34.json",
            AcceptableActions = new List<BotAction>
            {
                BotAction.Up,
                BotAction.Down,
                BotAction.Left,
                BotAction.Right,
            },
            TestDescription = "Test ClingyHeuroBot2 action on game state 34",
        };

        TestBotAction(_clingyHeuroBot2, testParams);
    }

    [Fact]
    public void GameState34MovementTest()
    {
        var testParams = new TestParams
        {
            TestName = "GameState34MovementTest",
            TestGameStateJsonPath = "34.json",
            AcceptableActions = new List<BotAction>
            {
                BotAction.Up,
                BotAction.Down,
                BotAction.Left,
                BotAction.Right,
            },
            TestDescription = "Test movement action validation",
        };

        TestBotAction(_clingyHeuroBot2, testParams);
    }

    [Fact]
    public void GameState34TickOverrideTest()
    {
        var testParams = new TestParams
        {
            TestName = "GameState34TickOverrideTest",
            TestGameStateJsonPath = "34.json",
            AcceptableActions = new List<BotAction>
            {
                BotAction.Up,
                BotAction.Down,
                BotAction.Left,
                BotAction.Right,
            },
            TickOverride = true,
            TestDescription = "Test tick override functionality",
        };

        TestTickOverride(testParams);
    }

    [Fact]
    public void ChaseImmediatePellet_LeftOrDown_EvenWhenChased()
    {
        var testParams = new TestParams
        {
            TestName = "TestGameState34WithExpectedLeftOrDownAction",
            TestGameStateJsonPath = "34.json",
            AcceptableActions = [BotAction.Down, BotAction.Left],
            BotNicknameToTest = "MarvijoClingyExpBot",
            ExpectedAction = null, // Allow either Left or Down action
            TestDescription =
                "Chase immediate pellet - should choose Left or Down even when chased",
            BotsArray = new object[] { _clingyHeuroBot2, _clingyHeuroBot },
        };

        TestBotsArray(testParams);
    }

    [Fact]
    public void ChaseMorePelletGroups()
    {
        var testParams = new TestParams
        {
            TestGameStateJsonPath = "162.json",
            BotNicknameToTest = "ClingyHeuroBot2",
            ExpectedAction = BotAction.Up, // Allow either Left or Down action
            BotsArray = new object[] { _clingyHeuroBot2, _clingyHeuroBot },
        };

        TestBotsArray(testParams);
    }
}
