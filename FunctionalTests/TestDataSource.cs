using System.Collections;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.FunctionalTests;

namespace FunctionalTests;

public class TestDataSource : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        var testDefinitions = BotTestHelper.LoadTestDefinitions();
        var allBots = BotFactory.GetAllBots().ToList(); //ToList to avoid multiple enumerations

        if (!allBots.Any())
        {
            // Yield a dummy object or throw to indicate no bots were loaded.
            // This prevents xUnit from erroring out if no tests are found due to no bots.
            // However, it might be better to let it fail if bot loading is critical.
            // For now, let's assume if no bots, no tests to run for this source.
            yield break;
        }

        foreach (var definition in testDefinitions)
        {
            var acceptableActions = definition
                .AcceptableActions.Select(actionStr =>
                    Enum.TryParse<BotAction>(actionStr, true, out var action)
                        ? action
                        : (BotAction?)null
                )
                .Where(action => action.HasValue)
                .Select(action => action!.Value)
                .ToList();

            if (!acceptableActions.Any())
            {
                // Log or handle cases where actions couldn't be parsed
                // For now, skip this test definition if no valid actions
                continue;
            }

            var testParamsBase = new FunctionalTestParams
            {
                TestName = definition.TestName,
                TestFileName = definition.GameStateFile,
                AcceptableActions = acceptableActions,
                // ExpectedAction can be set if the first acceptable action is considered the primary expected one
                ExpectedAction = acceptableActions.FirstOrDefault(),
            };

            if (!string.IsNullOrEmpty(definition.SpecificBotId))
            {
                // If the test definition is for a specific bot, try to find it.
                // This assumes SpecificBotId in JSON is the Guid string.
                var specificBot = allBots.FirstOrDefault(b =>
                    b.BotId.ToString()
                        .Equals(definition.SpecificBotId, StringComparison.OrdinalIgnoreCase)
                );
                if (specificBot != null)
                {
                    var specificTestParams = new FunctionalTestParams
                    {
                        TestName = $"{definition.TestName}_SpecificTo_{specificBot.GetType().Name}",
                        TestFileName = definition.GameStateFile,
                        AcceptableActions = acceptableActions,
                        ExpectedAction = acceptableActions.FirstOrDefault(),
                        BotIdToTest = definition.SpecificBotId, // Pass the specific bot ID
                    };
                    yield return new object[] { specificBot, specificTestParams };
                }
                else
                {
                    // Log that the specific bot was not found
                }
            }
            else
            {
                // If not for a specific bot, create a test case for each bot
                foreach (var bot in allBots)
                {
                    var botTestParams = new FunctionalTestParams
                    {
                        TestName = $"{definition.TestName}_{bot.GetType().Name}",
                        TestFileName = definition.GameStateFile,
                        AcceptableActions = acceptableActions,
                        ExpectedAction = acceptableActions.FirstOrDefault(),
                        BotIdToTest = bot.BotId.ToString(), // Use the bot's actual ID
                    };
                    yield return new object[] { bot, botTestParams };
                }
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
