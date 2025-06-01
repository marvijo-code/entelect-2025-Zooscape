using System.Collections;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using NSubstitute;
using Serilog;

namespace Marvijo.Zooscape.Bots.FunctionalTests;

public class TestDataSource : IEnumerable<object[]>
{
    private readonly ILogger _logger;

    public TestDataSource()
    {
        _logger = Substitute.For<ILogger>();
    }

    public IEnumerator<object[]> GetEnumerator()
    {
        var testDefinitions = BotTestHelper.LoadTestDefinitions();
        var allAvailableBots = BotFactory.GetAllBots(_logger).ToList();

        if (!allAvailableBots.Any())
        {
            _logger.Warning(
                "TestDataSource: No bots available from BotFactory. No tests will be generated."
            );
            yield break;
        }
        _logger.Information(
            $"TestDataSource: Found {allAvailableBots.Count} available bot types from BotFactory."
        );

        foreach (var definition in testDefinitions)
        {
            var acceptableActions = definition
                .CorrectActions.Select(actionStr =>
                    Enum.TryParse<BotAction>(actionStr, true, out var action)
                        ? action
                        : (BotAction?)null
                )
                .Where(action => action.HasValue)
                .Select(action => action!.Value)
                .ToList();

            if (!acceptableActions.Any())
            {
                _logger.Warning(
                    $"TestDataSource: Test definition '{definition.Name}' (file: {definition.StateFile}) has no parsable CorrectActions. Skipping."
                );
                continue;
            }

            List<IBot<GameState>> botsToTestForThisDefinition = new();

            if (definition.Bots != null && definition.Bots.Any())
            {
                foreach (var botEntry in definition.Bots)
                {
                    var foundBot = allAvailableBots.FirstOrDefault(b =>
                        b.GetType().Name.Equals(botEntry.Name, StringComparison.OrdinalIgnoreCase)
                        || b.GetType()
                            .Name.Replace("Bot", "", StringComparison.OrdinalIgnoreCase)
                            .Equals(botEntry.Name, StringComparison.OrdinalIgnoreCase)
                    );
                    if (foundBot != null)
                    {
                        botsToTestForThisDefinition.Add(foundBot);
                    }
                    else
                    {
                        _logger.Warning(
                            $"TestDataSource: Bot '{botEntry.Name}' specified in test definition '{definition.Name}' was not found in available bots. Skipping this entry."
                        );
                    }
                }
            }
            else
            {
                _logger.Information(
                    $"TestDataSource: Test definition '{definition.Name}' does not specify any bots. Applying to all {allAvailableBots.Count} available bots."
                );
                botsToTestForThisDefinition.AddRange(allAvailableBots);
            }

            if (!botsToTestForThisDefinition.Any())
            {
                _logger.Warning(
                    $"TestDataSource: No bots to test for definition '{definition.Name}' (file: {definition.StateFile}) after filtering. Skipping definition."
                );
                continue;
            }

            foreach (var bot in botsToTestForThisDefinition)
            {
                string? botIdInStateFile = null;
                if (definition.Bots != null && definition.Bots.Any())
                {
                    var entryForThisBot = definition.Bots.FirstOrDefault(be =>
                        be.Name.Equals(bot.GetType().Name, StringComparison.OrdinalIgnoreCase)
                        || be.Name.Equals(
                            bot.GetType()
                                .Name.Replace("Bot", "", StringComparison.OrdinalIgnoreCase),
                            StringComparison.OrdinalIgnoreCase
                        )
                    );
                    if (!string.IsNullOrEmpty(entryForThisBot?.Id))
                    {
                        botIdInStateFile = entryForThisBot.Id;
                        _logger.Information(
                            $"Test '{definition.Name}' for bot '{bot.GetType().Name}': Using explicit BotId '{botIdInStateFile}' from test_definitions.json for loading game state context."
                        );
                    }
                }

                var testParams = new FunctionalTestParams
                {
                    TestName = $"{definition.Name}_{bot.GetType().Name}",
                    TestFileName = definition.StateFile,
                    AcceptableActions = new List<BotAction>(acceptableActions),
                    ExpectedAction = acceptableActions.FirstOrDefault(),
                    BotIdToTest = botIdInStateFile,
                };
                yield return new object[] { bot, testParams };
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
