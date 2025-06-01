using System.Reflection;
using FunctionalTests.Models;
using FunctionalTests.Services;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace FunctionalTests.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly TestDefinitionLoader _testDefinitionLoader;
    private readonly BotFactory _botFactory;
    private readonly Serilog.ILogger _logger;

    public TestController()
    {
        _logger = Log.Logger ?? new LoggerConfiguration().WriteTo.Console().CreateLogger();
        _testDefinitionLoader = new TestDefinitionLoader(_logger);
        _botFactory = new BotFactory();
    }

    /// <summary>
    /// Get all available test definitions
    /// </summary>
    [HttpGet("definitions")]
    public ActionResult<List<TestDefinitionDto>> GetTestDefinitions()
    {
        try
        {
            var definitions = _testDefinitionLoader.LoadAllTestDefinitions();
            var dtos = definitions
                .Select(d => new TestDefinitionDto
                {
                    TestName = d.TestName,
                    GameStateFile = d.GameStateFile,
                    Description = d.Description,
                    BotNickname = d.BotNickname,
                    ExpectedAction = d.ExpectedAction?.ToString(),
                    AcceptableActions = d.AcceptableActions.Select(a => a.ToString()).ToList(),
                    TestType = d.TestType.ToString(),
                    TickOverride = d.TickOverride,
                    Bots = d.Bots,
                })
                .ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load test definitions");
            return StatusCode(500, $"Failed to load test definitions: {ex.Message}");
        }
    }

    /// <summary>
    /// Get a specific game state for visualization
    /// </summary>
    [HttpGet("gamestate/{fileName}")]
    public ActionResult<object> GetGameState(string fileName)
    {
        try
        {
            var gameState = BotTestHelper.LoadGameState(fileName, _logger);
            return Ok(gameState);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load game state {FileName}", fileName);
            return NotFound($"Game state {fileName} not found: {ex.Message}");
        }
    }

    /// <summary>
    /// Run a specific test by name
    /// </summary>
    [HttpPost("run/{testName}")]
    public ActionResult<TestResultDto> RunTest(string testName)
    {
        try
        {
            var definitions = _testDefinitionLoader.LoadAllTestDefinitions();
            var definition = definitions.FirstOrDefault(d => d.TestName == testName);

            if (definition == null)
            {
                return NotFound($"Test '{testName}' not found");
            }

            var result = ExecuteTest(definition);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to run test {TestName}", testName);
            return Ok(
                new TestResultDto
                {
                    TestName = testName,
                    Success = false,
                    ErrorMessage = ex.Message,
                    ExecutionTimeMs = 0,
                    BotResults = [],
                }
            );
        }
    }

    /// <summary>
    /// Run all tests
    /// </summary>
    [HttpPost("run/all")]
    public ActionResult<List<TestResultDto>> RunAllTests()
    {
        try
        {
            var definitions = _testDefinitionLoader.LoadAllTestDefinitions();
            var results = new List<TestResultDto>();

            foreach (var definition in definitions)
            {
                try
                {
                    var result = ExecuteTest(definition);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to run test {TestName}", definition.TestName);
                    results.Add(
                        new TestResultDto
                        {
                            TestName = definition.TestName,
                            Success = false,
                            ErrorMessage = ex.Message,
                            ExecutionTimeMs = 0,
                            BotResults = [],
                        }
                    );
                }
            }

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to run all tests");
            return StatusCode(500, $"Failed to run tests: {ex.Message}");
        }
    }

    private TestResultDto ExecuteTest(TestDefinition definition)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var testParams = _testDefinitionLoader.ConvertToTestParams(definition);
        var gameState = BotTestHelper.LoadGameState(testParams.TestGameStateJsonPath, _logger);
        var botResults = new List<BotResultDto>();

        try
        {
            switch (definition.TestType)
            {
                case TestType.GameStateLoad:
                    // Just loading the game state is the test
                    break;

                case TestType.SingleBot:
                    var bot = _botFactory.CreateBot("ClingyHeuroBot2");
                    var result = ExecuteBotTest(bot, "ClingyHeuroBot2", gameState, testParams);
                    botResults.Add(result);
                    break;

                case TestType.MultiBotArray:
                    if (definition.Bots.Count == 0)
                    {
                        throw new InvalidOperationException(
                            $"Test {definition.TestName} has TestType.MultiBotArray but no bots specified"
                        );
                    }

                    foreach (var botType in definition.Bots)
                    {
                        var botInstance = _botFactory.CreateBot(botType);
                        var botResult = ExecuteBotTest(botInstance, botType, gameState, testParams);
                        botResults.Add(botResult);
                    }
                    break;

                case TestType.TickOverride:
                    // For tick override, we just validate the game state has a tick
                    if (gameState.Tick <= 0)
                    {
                        throw new InvalidOperationException(
                            "Game state does not have a valid tick for tick override test"
                        );
                    }
                    break;

                default:
                    throw new ArgumentException($"Unknown test type: {definition.TestType}");
            }

            stopwatch.Stop();
            return new TestResultDto
            {
                TestName = definition.TestName,
                Success = true,
                ErrorMessage = null,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                BotResults = botResults,
                GameStateFile = definition.GameStateFile,
                TestType = definition.TestType.ToString(),
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            throw new Exception($"Test execution failed: {ex.Message}", ex);
        }
    }

    private BotResultDto ExecuteBotTest(
        object bot,
        string botType,
        Marvijo.Zooscape.Bots.Common.Models.GameState gameState,
        TestParams testParams
    )
    {
        try
        {
            // Set up bot ID
            var testBotId = Guid.NewGuid();

            // Find animal by nickname if specified
            if (!string.IsNullOrEmpty(testParams.BotNicknameToTest))
            {
                var animal = gameState.Animals.FirstOrDefault(a =>
                    a.Nickname?.Equals(
                        testParams.BotNicknameToTest,
                        StringComparison.OrdinalIgnoreCase
                    ) == true
                );
                if (animal != null)
                {
                    testBotId = animal.Id;
                }
            }

            // Set bot ID and get action
            var action = GetBotAction(bot, botType, testBotId, gameState);

            // Validate action
            var isValid = ValidateAction(action, testParams);

            return new BotResultDto
            {
                BotType = botType,
                Action = action.ToString(),
                Success = isValid,
                ErrorMessage = isValid ? null : $"Action {action} not acceptable",
                BotId = testBotId.ToString(),
            };
        }
        catch (Exception ex)
        {
            return new BotResultDto
            {
                BotType = botType,
                Action = null,
                Success = false,
                ErrorMessage = ex.Message,
                BotId = Guid.NewGuid().ToString(),
            };
        }
    }

    private Marvijo.Zooscape.Bots.Common.Enums.BotAction GetBotAction(
        object bot,
        string botType,
        Guid botId,
        Marvijo.Zooscape.Bots.Common.Models.GameState gameState
    )
    {
        // Use reflection to set BotId and get action
        var botIdProperty = bot.GetType().GetProperty("BotId");
        if (botIdProperty != null)
        {
            botIdProperty.SetValue(bot, botId);
        }

        var getActionMethod = bot.GetType().GetMethod("GetAction");
        if (getActionMethod != null)
        {
            var result = getActionMethod.Invoke(bot, new object[] { gameState });
            if (result is Marvijo.Zooscape.Bots.Common.Enums.BotAction action)
            {
                return action;
            }
        }

        throw new InvalidOperationException($"Could not get action from bot {botType}");
    }

    private bool ValidateAction(
        Marvijo.Zooscape.Bots.Common.Enums.BotAction action,
        TestParams testParams
    )
    {
        // If expected action is specified, must match exactly
        if (testParams.ExpectedAction.HasValue)
        {
            return action == testParams.ExpectedAction.Value;
        }

        // If acceptable actions are specified, must be in the list
        if (testParams.AcceptableActions.Any())
        {
            return testParams.AcceptableActions.Contains(action);
        }

        // If no constraints, any action is valid
        return true;
    }
}
