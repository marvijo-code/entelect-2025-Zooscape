using System.Reflection;
using FunctionalTests.Models;
using FunctionalTests.Services;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;
using Xunit;
using Xunit.Abstractions;

namespace FunctionalTests;

/// <summary>
/// JSON-driven functional tests that load test definitions from JSON files
/// </summary>
public class JsonDrivenTests : BotTestsBase
{
    private readonly TestDefinitionLoader _testDefinitionLoader;
    private readonly BotFactory _botFactory;

    public JsonDrivenTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
        _testDefinitionLoader = new TestDefinitionLoader(_logger);
        _botFactory = new BotFactory();
    }

    [Fact]
    public void ExecuteAllJsonDefinedTests()
    {
        // Load all test definitions from JSON files
        var testDefinitions = _testDefinitionLoader.LoadAllTestDefinitions();

        Assert.NotEmpty(testDefinitions);
        _logger.Information("Executing {Count} JSON-defined tests", testDefinitions.Count);

        var failedTests = new List<string>();
        var passedTests = new List<string>();

        foreach (var definition in testDefinitions)
        {
            try
            {
                _logger.Information("Executing test: {TestName}", definition.TestName);
                ExecuteTestDefinition(definition);
                passedTests.Add(definition.TestName);
                _logger.Information("✅ Test passed: {TestName}", definition.TestName);
            }
            catch (Exception ex)
            {                failedTests.Add($"{definition.TestName}: {ex.Message}");
                _logger.Error(ex, "❌ Test failed: {TestName}", definition.TestName);
            }
        }

        // Report summary
        _logger.Information(
            "Test Summary: {PassedCount} passed, {FailedCount} failed",
            passedTests.Count,
            failedTests.Count
        );

        if (passedTests.Count > 0)
        {
            _logger.Information("Passed tests: {PassedTests}", string.Join(", ", passedTests));
        }

        if (failedTests.Count > 0)
        {
            _logger.Error("Failed tests: {FailedTests}", string.Join("; ", failedTests));
            Assert.Fail($"Failed tests: {string.Join("; ", failedTests)}");
        }
    }

    [Theory]
    [ClassData(typeof(TestDefinitionDataSource))]
    public void ExecuteIndividualJsonTest(TestDefinition definition)
    {
        _logger.Information("Executing individual test: {TestName}", definition.TestName);
        ExecuteTestDefinition(definition);
        _logger.Information("✅ Individual test passed: {TestName}", definition.TestName);
    }

    private string GetSourcePath(string subfolder = "")
    {
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var currentDir = new DirectoryInfo(Path.GetDirectoryName(assemblyLocation));

        while (currentDir != null && !currentDir.GetFiles("*.sln").Any())
        {
            currentDir = currentDir.Parent;
        }

        if (currentDir == null)
        {
            _logger.Error("Could not find solution root. Falling back to current directory.");
            return Path.Combine(Directory.GetCurrentDirectory(), "FunctionalTests", subfolder);
        }

        var projectPath = Path.Combine(currentDir.FullName, "FunctionalTests");
        return !string.IsNullOrEmpty(subfolder) ? Path.Combine(projectPath, subfolder) : projectPath;
    }

    private void ExecuteTestDefinition(TestDefinition definition)
    {
        var testParams = _testDefinitionLoader.ConvertToTestParams(definition);
        var gameStatesPath = GetSourcePath("GameStates");
        var gameStateFilePath = Path.Combine(gameStatesPath, testParams.TestGameStateJsonPath);

        _logger.Information("Attempting to load game state from: {Path}", gameStateFilePath);

        if (!File.Exists(gameStateFilePath))
        {
            _logger.Error("Game state file not found at resolved path: {Path}", gameStateFilePath);
            throw new FileNotFoundException("Game state file not found.", gameStateFilePath);
        }

        switch (definition.TestType)
        {
            case TestType.GameStateLoad:
                ValidateGameStateLoading(testParams);
                break;

            case TestType.SingleBot:
                if (string.IsNullOrEmpty(definition.BotNickname))
                {
                    throw new InvalidOperationException($"Test {definition.TestName} is of type SingleBot but has no BotNickname specified.");
                }
                var bot = _botFactory.CreateBot(definition.BotNickname);

                // Enable heuristic logging for supported bots
                if (bot is StaticHeuro.Services.HeuroBotService staticHeuroBot)
                {
                    staticHeuroBot.LogHeuristicScores = true;
                }
                else if (bot is ClingyHeuroBot2.Services.HeuroBotService clingyHeuroBot2)
                {
                    clingyHeuroBot2.LogHeuristicScores = true;
                }
                TestBotFromArray(
                    bot,
                    _gameStateLoader.LoadGameState(gameStateFilePath),
                    testParams
                );
                break;

            case TestType.MultiBotArray:
                if (definition.Bots.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Test {definition.TestName} has TestType.MultiBotArray but no bots specified"
                    );
                }

                var bots = _botFactory.CreateBots(definition.Bots);
                var testParamsWithBots = new TestParams
                {
                    TestName = testParams.TestName,
                    TestGameStateJsonPath = testParams.TestGameStateJsonPath,
                    TestDescription = testParams.TestDescription,
                    BotNicknameToTest = testParams.BotNicknameToTest,
                    ExpectedAction = testParams.ExpectedAction,
                    AcceptableActions = testParams.AcceptableActions,
                    TickOverride = testParams.TickOverride,
                    BotsArray = bots,
                };
                TestBotsArray(testParamsWithBots);
                break;

            case TestType.TickOverride:
                TestTickOverride(testParams);
                break;

            default:
                throw new ArgumentException($"Unknown test type: {definition.TestType}");
        }
    }
}
