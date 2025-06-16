using System.Text.Json;
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
    /// Get all available bot types
    /// </summary>
    [HttpGet("bots")]
    public ActionResult<List<string>> GetAvailableBots()
    {
        try
        {
            var availableBots = _botFactory.GetAvailableBotTypes();
            return Ok(availableBots);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get available bots");
            return StatusCode(500, $"Failed to get available bots: {ex.Message}");
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
    /// Create a new test definition dynamically
    /// </summary>
    [HttpPost("create")]
    public ActionResult<TestDefinitionDto> CreateTest([FromBody] CreateTestRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.TestName))
            {
                return BadRequest("Test name is required");
            }

            if (string.IsNullOrWhiteSpace(request.GameStateFile))
            {
                return BadRequest("Game state file is required");
            }

            // Parse TestType from string
            if (!Enum.TryParse<TestType>(request.TestType, true, out var testType))
            {
                return BadRequest($"Invalid test type: {request.TestType}");
            }

            string gameStateFileName = request.GameStateFile;

            // If current game state is provided, save it to GameStates directory
            if (request.CurrentGameState != null)
            {
                try
                {
                    // Create a unique filename with tick number if available
                    var gameStateJson = System.Text.Json.JsonSerializer.Serialize(
                        request.CurrentGameState,
                        new JsonSerializerOptions { WriteIndented = true }
                    );

                    // Try to extract tick number from the game state
                    var gameStateDict = request.CurrentGameState as System.Text.Json.JsonElement?;
                    var tickNumber = 0;

                    if (gameStateDict.HasValue)
                    {
                        if (gameStateDict.Value.TryGetProperty("tick", out var tickProp))
                        {
                            tickNumber = tickProp.GetInt32();
                        }
                        else if (
                            gameStateDict.Value.TryGetProperty("Tick", out var tickPropCapital)
                        )
                        {
                            tickNumber = tickPropCapital.GetInt32();
                        }
                    }

                    // Create filename with tick number
                    var gameStateBaseName = Path.GetFileNameWithoutExtension(request.GameStateFile);
                    gameStateFileName = $"{gameStateBaseName}_{tickNumber}.json";

                    // Save to GameStates directory
                    var gameStatesPath = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "GameStates"
                    );
                    if (!Directory.Exists(gameStatesPath))
                    {
                        Directory.CreateDirectory(gameStatesPath);
                    }

                    var gameStateFilePath = Path.Combine(gameStatesPath, gameStateFileName);

                    // Check for duplicate game state file and create unique name if needed
                    var originalFileName = gameStateFileName;
                    var counter = 1;
                    while (System.IO.File.Exists(gameStateFilePath))
                    {
                        gameStateFileName = $"{gameStateBaseName}_{tickNumber}_{counter}.json";
                        gameStateFilePath = Path.Combine(gameStatesPath, gameStateFileName);
                        counter++;
                    }

                    if (originalFileName != gameStateFileName)
                    {
                        _logger.Information(
                            "Game state file {OriginalName} already exists, using {NewName}",
                            originalFileName,
                            gameStateFileName
                        );
                    }

                    System.IO.File.WriteAllText(gameStateFilePath, gameStateJson);

                    _logger.Information("Saved game state to: {FilePath}", gameStateFilePath);
                }
                catch (Exception ex)
                {
                    _logger.Warning(
                        ex,
                        "Failed to save current game state, using original filename"
                    );
                    gameStateFileName = request.GameStateFile;
                }
            }

            // Create new test definition
            var testDefinition = new TestDefinition
            {
                TestName = request.TestName,
                GameStateFile = gameStateFileName, // Use the potentially modified filename
                Description =
                    request.Description ?? $"Dynamically created test for {gameStateFileName}",
                BotNickname = request.BotNickname,
                ExpectedAction = request.ExpectedAction,
                AcceptableActions =
                    request.AcceptableActions
                    ?? new List<Marvijo.Zooscape.Bots.Common.Enums.BotAction>(),
                TestType = testType,
                TickOverride = request.TickOverride,
                Bots = request.Bots ?? new List<string>(),
            };

            // Save the test definition
            _testDefinitionLoader.SaveTestDefinition(testDefinition);

            // Convert to DTO for response
            var dto = new TestDefinitionDto
            {
                TestName = testDefinition.TestName,
                GameStateFile = testDefinition.GameStateFile,
                Description = testDefinition.Description,
                BotNickname = testDefinition.BotNickname,
                ExpectedAction = testDefinition.ExpectedAction?.ToString(),
                AcceptableActions = testDefinition
                    .AcceptableActions.Select(a => a.ToString())
                    .ToList(),
                TestType = testDefinition.TestType.ToString(),
                TickOverride = testDefinition.TickOverride,
                Bots = testDefinition.Bots,
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create test {TestName}", request.TestName);
            return StatusCode(500, $"Failed to create test: {ex.Message}");
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
            var initialScore = 0;

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
                    initialScore = animal.Score;
                }
            }
            else
            {
                // Use first animal if no specific nickname
                var firstAnimal = gameState.Animals.FirstOrDefault();
                if (firstAnimal != null)
                {
                    testBotId = firstAnimal.Id;
                    initialScore = firstAnimal.Score;
                }
            }

            // Set bot ID and get action with scores
            var (action, actionScores) = GetBotActionWithScores(bot, botType, testBotId, gameState);

            // Validate action
            var isValid = ValidateAction(action, testParams);

            // Get current animal state for score tracking
            var currentAnimal = gameState.Animals.FirstOrDefault(a => a.Id == testBotId);
            var chosenActionScore = actionScores.ContainsKey(action) ? actionScores[action] : 0;

            var performanceMetrics = new Dictionary<string, object>
            {
                ["actionType"] = action.ToString(),
                ["gameStateTick"] = gameState.Tick,
                ["totalAnimals"] = gameState.Animals.Count,
                ["totalCells"] = gameState.Cells.Count,
                ["animalPosition"] =
                    currentAnimal != null ? $"({currentAnimal.X}, {currentAnimal.Y})" : "unknown",
                ["cellsWithPellets"] = gameState.Cells.Count(c => (int)c.Content == 2),
                ["chosenActionScore"] = chosenActionScore,
                ["allActionScores"] = actionScores.ToDictionary(
                    kvp => kvp.Key.ToString(),
                    kvp => kvp.Value
                ),
            };

            return new BotResultDto
            {
                BotType = botType,
                Action = action.ToString(),
                Success = isValid,
                ErrorMessage = isValid ? null : $"Action {action} not acceptable",
                BotId = testBotId.ToString(),
                InitialScore = initialScore,
                FinalScore = (int)chosenActionScore, // Use the bot's calculated score for this action
                ScoreDelta = (int)chosenActionScore - initialScore,
                PerformanceMetrics = performanceMetrics,
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
                InitialScore = 0,
                FinalScore = 0,
                ScoreDelta = 0,
                PerformanceMetrics = new(),
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

    private (
        Marvijo.Zooscape.Bots.Common.Enums.BotAction action,
        Dictionary<Marvijo.Zooscape.Bots.Common.Enums.BotAction, decimal> actionScores
    ) GetBotActionWithScores(
        object bot,
        string botType,
        Guid botId,
        Marvijo.Zooscape.Bots.Common.Models.GameState gameState
    )
    {
        // Use reflection to set BotId
        var botIdProperty = bot.GetType().GetProperty("BotId");
        if (botIdProperty != null)
        {
            botIdProperty.SetValue(bot, botId);
        }

        // Try to get action with scores first (for ClingyHeuroBot2)
        var getActionWithScoresMethod = bot.GetType().GetMethod("GetActionWithScores");
        if (getActionWithScoresMethod != null)
        {
            var result = getActionWithScoresMethod.Invoke(bot, new object[] { gameState });
            if (result != null)
            {
                // Extract the tuple components using reflection
                var resultType = result.GetType();

                // Try to get ChosenAction (either field or property)
                object? chosenAction = null;
                var chosenActionField = resultType.GetField("ChosenAction");
                if (chosenActionField != null)
                {
                    chosenAction = chosenActionField.GetValue(result);
                }
                else
                {
                    var chosenActionProperty = resultType.GetProperty("ChosenAction");
                    if (chosenActionProperty != null)
                    {
                        chosenAction = chosenActionProperty.GetValue(result);
                    }
                }

                // Try to get ActionScores (either field or property)
                object? actionScores = null;
                var actionScoresField = resultType.GetField("ActionScores");
                if (actionScoresField != null)
                {
                    actionScores = actionScoresField.GetValue(result);
                }
                else
                {
                    var actionScoresProperty = resultType.GetProperty("ActionScores");
                    if (actionScoresProperty != null)
                    {
                        actionScores = actionScoresProperty.GetValue(result);
                    }
                }

                if (
                    chosenAction is Marvijo.Zooscape.Bots.Common.Enums.BotAction action
                    && actionScores
                        is Dictionary<Marvijo.Zooscape.Bots.Common.Enums.BotAction, decimal> scores
                )
                {
                    return (action, scores);
                }
            }
        }

        // Fallback to regular GetAction method
        var getActionMethod = bot.GetType().GetMethod("GetAction");
        if (getActionMethod != null)
        {
            var result = getActionMethod.Invoke(bot, new object[] { gameState });
            if (result is Marvijo.Zooscape.Bots.Common.Enums.BotAction action)
            {
                // Return empty scores dictionary if bot doesn't support action scoring
                return (
                    action,
                    new Dictionary<Marvijo.Zooscape.Bots.Common.Enums.BotAction, decimal>()
                );
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
