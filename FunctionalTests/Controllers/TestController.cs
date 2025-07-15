using System.Text.Json;
using FunctionalTests.Models;
using Microsoft.Extensions.Logging;
using FunctionalTests.Services;
using Marvijo.Zooscape.Bots.Common.Models;
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
                    _logger.Error(
                        ex,
                        "Failed to save current game state. Aborting test creation."
                    );
                    return StatusCode(500, "Failed to save game state file, cannot create test.");
                }
            }

            // Create new test definition
            var testDefinition = new TestDefinition
            {
                TestName = request.TestName,
                GameStateFile = gameStateFileName, // Use the potentially modified filename
                Description =
                    request.Description ?? $"Dynamically created test for {gameStateFileName}",
                BotNickname = request.BotNicknameInState,
                ExpectedAction = request.ExpectedAction,
                AcceptableActions =
                    request.AcceptableActions
                    ?? new List<Marvijo.Zooscape.Bots.Common.Enums.BotAction>(),
                TestType = testType,
                TickOverride = request.TickOverride,
                Bots = request.BotsToTest ?? new List<string>(),
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
    /// Run a test directly without saving test definition
    /// </summary>
    [HttpPost("direct")]
    public ActionResult<TestResultDto> RunTestDirect([FromBody] CreateTestRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.TestName))
            {
                return BadRequest("Test name is required");
            }

            // Parse TestType from string
            if (!Enum.TryParse<TestType>(request.TestType, true, out var testType))
            {
                return BadRequest($"Invalid test type: {request.TestType}");
            }

            // Create temporary test definition without saving
            var testDefinition = new TestDefinition
            {
                TestName = request.TestName,
                GameStateFile = request.GameStateFile ?? "temp-state.json",
                Description =
                    request.Description ?? $"Direct test execution for {request.TestName}",
                BotNickname = request.BotNicknameInState,
                ExpectedAction = request.ExpectedAction,
                AcceptableActions =
                    request.AcceptableActions
                    ?? new List<Marvijo.Zooscape.Bots.Common.Enums.BotAction>(),
                TestType = testType,
                TickOverride = request.TickOverride,
                Bots = request.BotsToTest ?? new List<string>(),
            };

            // Execute test directly with provided game state
            var result = ExecuteTestDirect(testDefinition, request.CurrentGameState);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to run direct test {TestName}", request.TestName);
            return Ok(
                new TestResultDto
                {
                    TestName = request.TestName,
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

    [HttpPost("search-state-files")]
    public IActionResult SearchStateFiles([FromBody] StateFileSearchRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.BotNickname))
        {
            return BadRequest("BotNickname is required");
        }

        var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "logs");
        if (!Directory.Exists(logsDir))
        {
            return NotFound(new { error = "Logs directory not found" });
        }

        var matchingFiles = new List<string>();

        // Iterate through each game log directory
        foreach (var gameDir in Directory.GetDirectories(logsDir))
        {
            // Get all JSON files in this game directory
            var jsonFiles = Directory.GetFiles(gameDir, "*.json");
            foreach (var jsonFile in jsonFiles)
            {
                // Extract tick number from filename (e.g., "tick_123.json")
                var fileName = Path.GetFileNameWithoutExtension(jsonFile);
                if (int.TryParse(fileName.Split('_').Last(), out int tick))
                {
                    // Skip if tick is less than or equal to the min tick
                    if (tick <= request.MinTick)
                    {
                        continue;
                    }
                }

                // Check if the file contains the bot nickname using streaming
                bool containsNickname = false;
                using (var stream = System.IO.File.OpenRead(jsonFile))
                using (var reader = new System.IO.StreamReader(stream))
                {
                    const int bufferSize = 4096;
                    char[] buffer = new char[bufferSize];
                    int bytesRead;
                    string contentSoFar = "";
                    while ((bytesRead = reader.Read(buffer, 0, bufferSize)) > 0)
                    {
                        contentSoFar += new string(buffer, 0, bytesRead);
                        if (contentSoFar.Contains(request.BotNickname))
                        {
                            containsNickname = true;
                            break;
                        }
                        // Prevent memory bloat by keeping only the last part that might be split
                        if (contentSoFar.Length > request.BotNickname.Length * 2)
                        {
                            contentSoFar = contentSoFar.Substring(
                                contentSoFar.Length - request.BotNickname.Length * 2
                            );
                        }
                    }
                }

                if (containsNickname)
                {
                    try
                    {
                        var content = System.IO.File.ReadAllText(jsonFile);
                        var state = JsonSerializer.Deserialize<GameState>(content);
                        if (
                            state != null
                            && state.Tick >= request.MinTick
                            && state.Tick <= request.MaxTick
                        )
                        {
                            matchingFiles.Add(jsonFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Error processing state file: {jsonFile}");
                    }
                }
            }
        }

        // Apply paging
        var skip = (request.PageNumber - 1) * request.PageSize;
        var take = request.PageSize;
        var pagedFiles = matchingFiles.Skip(skip).Take(take).ToList();

        // Return total count for pagination
        var totalCount = matchingFiles.Count;
        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

        return Ok(
            new
            {
                files = pagedFiles,
                totalCount,
                totalPages,
                pageNumber = request.PageNumber,
                pageSize = request.PageSize,
            }
        );
    }

    private TestResultDto ExecuteTestDirect(TestDefinition definition, object currentGameState)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var botResults = new List<BotResultDto>();

        try
        {
            // Convert the provided game state to the expected format
            Marvijo.Zooscape.Bots.Common.Models.GameState gameState;

            if (currentGameState != null)
            {
                // Serialize and deserialize to convert to the expected type
                var jsonString = System.Text.Json.JsonSerializer.Serialize(currentGameState);
                gameState =
                    System.Text.Json.JsonSerializer.Deserialize<Marvijo.Zooscape.Bots.Common.Models.GameState>(
                        jsonString
                    );
            }
            else
            {
                throw new Exception("No game state provided for direct execution");
            }

            var testParams = new TestParams
            {
                TestName = definition.TestName,
                TestGameStateJsonPath = definition.GameStateFile,
                AcceptableActions = definition.AcceptableActions,
                BotNicknameToTest = definition.BotNickname,
                ExpectedAction = definition.ExpectedAction,
            };

            switch (definition.TestType)
            {
                case TestType.SingleBot:
                    if (definition.Bots != null && definition.Bots.Any())
                    {
                        foreach (var botType in definition.Bots)
                        {
                            var bot = _botFactory.CreateBot(botType);
                            if (bot != null)
                            {
                                var result = ExecuteBotTest(bot, botType, gameState, testParams);
                                botResults.Add(result);
                                break; // Only test first bot for SingleBot
                            }
                        }
                    }
                    break;

                case TestType.MultiBotArray:
                    if (definition.Bots != null && definition.Bots.Any())
                    {
                        foreach (var botType in definition.Bots)
                        {
                            var bot = _botFactory.CreateBot(botType);
                            if (bot != null)
                            {
                                var result = ExecuteBotTest(bot, botType, gameState, testParams);
                                botResults.Add(result);
                            }
                        }
                    }
                    break;

                case TestType.GameStateLoad:
                    // Just verify the game state loaded successfully
                    botResults.Add(
                        new BotResultDto
                        {
                            BotType = "GameStateLoader",
                            Success = true,
                            Action = "None",
                            ErrorMessage = null,
                            PerformanceMetrics = new Dictionary<string, object>
                            {
                                ["GameStateLoaded"] = true,
                                ["AnimalsCount"] = gameState.Animals?.Count ?? 0,
                                ["CellsCount"] = gameState.Cells?.Count ?? 0,
                            },
                        }
                    );
                    break;

                default:
                    throw new NotSupportedException(
                        $"Test type {definition.TestType} not supported for direct execution"
                    );
            }

            stopwatch.Stop();

            return new TestResultDto
            {
                TestName = definition.TestName,
                Success = botResults.All(r => r.Success),
                ErrorMessage = botResults.Any(r => !r.Success)
                    ? string.Join(
                        "; ",
                        botResults.Where(r => !r.Success).Select(r => r.ErrorMessage)
                    )
                    : null,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                BotResults = botResults,
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex, "Error executing direct test {TestName}", definition.TestName);

            return new TestResultDto
            {
                TestName = definition.TestName,
                Success = false,
                ErrorMessage = ex.Message,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                BotResults = botResults,
            };
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
                    if (string.IsNullOrEmpty(definition.BotNickname))
                    {
                        throw new InvalidOperationException($"Test {definition.TestName} is of type SingleBot but has no BotNickname specified.");
                    }
                    var bot = _botFactory.CreateBot(definition.BotNickname);
                    var result = ExecuteBotTest(bot, definition.BotNickname, gameState, testParams);
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
            var (action, actionScores, detailedScores) = GetBotActionWithDetailedScores(
                bot,
                botType,
                testBotId,
                gameState
            );

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
                ["detailedHeuristicScores"] = detailedScores ?? new List<object>(),
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
            _logger.Error(ex.InnerException ?? ex, "Bot execution failed for {BotType}", botType);
            return new BotResultDto
            {
                BotType = botType,
                Action = null,
                Success = false,
                ErrorMessage = (ex.InnerException ?? ex).ToString(), // Capture the full inner exception details
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
        Dictionary<Marvijo.Zooscape.Bots.Common.Enums.BotAction, decimal> actionScores,
        List<object>? detailedScores
    ) GetBotActionWithDetailedScores(
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

        // Enable heuristic logging for detailed scores
        var logHeuristicScoresProperty = bot.GetType().GetProperty("LogHeuristicScores");
        if (logHeuristicScoresProperty != null)
        {
            logHeuristicScoresProperty.SetValue(bot, true);
        }

        // Try to get action with detailed scores first (for ClingyHeuroBot2)
        var getActionWithDetailedScoresMethod = bot.GetType()
            .GetMethod("GetActionWithDetailedScores");
        if (getActionWithDetailedScoresMethod != null)
        {
            var result = getActionWithDetailedScoresMethod.Invoke(bot, new object[] { gameState });
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

                // Try to get DetailedScores (either field or property)
                object? detailedScores = null;
                var detailedScoresField = resultType.GetField("DetailedScores");
                if (detailedScoresField != null)
                {
                    detailedScores = detailedScoresField.GetValue(result);
                }
                else
                {
                    var detailedScoresProperty = resultType.GetProperty("DetailedScores");
                    if (detailedScoresProperty != null)
                    {
                        detailedScores = detailedScoresProperty.GetValue(result);
                    }
                }

                if (
                    chosenAction is Marvijo.Zooscape.Bots.Common.Enums.BotAction action
                    && actionScores
                        is Dictionary<Marvijo.Zooscape.Bots.Common.Enums.BotAction, decimal> scores
                )
                {
                    // Convert detailed scores to a generic list
                    List<object>? convertedDetailedScores = null;
                    if (detailedScores != null)
                    {
                        convertedDetailedScores = new List<object>();
                        if (detailedScores is System.Collections.IEnumerable enumerable)
                        {
                            foreach (var scoreLog in enumerable)
                            {
                                if (scoreLog != null)
                                {
                                    var scoreLogType = scoreLog.GetType();
                                    var moveProperty = scoreLogType.GetProperty("Move");
                                    var totalScoreProperty = scoreLogType.GetProperty("TotalScore");
                                    var detailedLogLinesProperty = scoreLogType.GetProperty(
                                        "DetailedLogLines"
                                    );

                                    var actionValue =
                                        moveProperty?.GetValue(scoreLog)?.ToString() ?? "Unknown";
                                    var totalScoreValue =
                                        totalScoreProperty?.GetValue(scoreLog) ?? 0;
                                    var detailedLogLinesValue =
                                        detailedLogLinesProperty?.GetValue(scoreLog) as List<string>
                                        ?? new List<string>();

                                    convertedDetailedScores.Add(
                                        new
                                        {
                                            Action = actionValue,
                                            TotalScore = totalScoreValue,
                                            DetailedLogLines = detailedLogLinesValue,
                                        }
                                    );
                                }
                            }
                        }
                    }

                    return (action, scores, convertedDetailedScores);
                }
            }
        }

        // Fallback to regular GetActionWithScores method
        var (fallbackAction, fallbackScores) = GetBotActionWithScores(
            bot,
            botType,
            botId,
            gameState
        );
        return (fallbackAction, fallbackScores, null);
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

        // Try to get action with detailed scores first (for ClingyHeuroBot2)
        var getActionWithDetailedScoresMethod = bot.GetType()
            .GetMethod("GetActionWithDetailedScores");
        if (getActionWithDetailedScoresMethod != null)
        {
            var result = getActionWithDetailedScoresMethod.Invoke(bot, new object[] { gameState });
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

        // Fallback to try GetActionWithScores method
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

public class StateFileSearchRequest
{
    public string BotNickname { get; set; }
    public int MinTick { get; set; } = 0;
    public int MaxTick { get; set; } = int.MaxValue;
    public int MaxResults { get; set; } = 10;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 100;
}
