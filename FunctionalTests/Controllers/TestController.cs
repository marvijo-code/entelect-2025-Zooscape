using System.Text.Json;
using FunctionalTests.Models;
using FunctionalTests.Services;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace FunctionalTests.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    public class StateFileSearchRequest
    {
        public required string BotNickname { get; set; }
        public int MinTick { get; set; } = 0;
        public int MaxTick { get; set; } = int.MaxValue;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    private readonly TestDefinitionLoader _testDefinitionLoader;
    private readonly BotFactory _botFactory;
    private readonly Serilog.ILogger _logger;

    public TestController()
    {
        _logger = Log.Logger ?? new LoggerConfiguration().WriteTo.Console().CreateLogger();
        _testDefinitionLoader = new TestDefinitionLoader(_logger);
        _botFactory = new BotFactory();
    }

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
                    BotNicknameInStateFile = d.BotNicknameInStateFile,
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
                    BotResults = new List<BotResultDto>(),
                }
            );
        }
    }

    [HttpPost("create")]
    public ActionResult<TestDefinitionDto> CreateTest([FromBody] CreateTestRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.TestName))
            {
                return BadRequest("Test name is required");
            }

            if (!Enum.TryParse<TestType>(request.TestType, true, out var testType))
            {
                return BadRequest($"Invalid test type: {request.TestType}");
            }

            string gameStateFileName = request.GameStateFile;
            if (request.CurrentGameState != null)
            {
                try
                {
                    var gameStateJson = JsonSerializer.Serialize(request.CurrentGameState, new JsonSerializerOptions { WriteIndented = true });
                    var gameStateDict = request.CurrentGameState as JsonElement?;
                    var tickNumber = 0;
                    if (gameStateDict.HasValue && gameStateDict.Value.TryGetProperty("tick", out var tickProp))
                    {
                        tickNumber = tickProp.GetInt32();
                    }

                    var gameStateBaseName = Path.GetFileNameWithoutExtension(request.GameStateFile);
                    gameStateFileName = $"{gameStateBaseName}_{tickNumber}.json";
                    var gameStatesPath = Path.Combine(Directory.GetCurrentDirectory(), "GameStates");
                    if (!Directory.Exists(gameStatesPath))
                    {
                        Directory.CreateDirectory(gameStatesPath);
                    }
                    var gameStateFilePath = Path.Combine(gameStatesPath, gameStateFileName);
                    System.IO.File.WriteAllText(gameStateFilePath, gameStateJson);
                    _logger.Information("Saved game state to: {FilePath}", gameStateFilePath);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to save current game state. Aborting test creation.");
                    return StatusCode(500, "Failed to save game state file, cannot create test.");
                }
            }

            var testDefinition = new TestDefinition
            {
                TestName = request.TestName,
                GameStateFile = gameStateFileName,
                Description = request.Description ?? $"Dynamically created test for {gameStateFileName}",
                BotNicknameInStateFile = request.BotNicknameInState,
                ExpectedAction = request.ExpectedAction,
                AcceptableActions = request.AcceptableActions ?? new List<Marvijo.Zooscape.Bots.Common.Enums.BotAction>(),
                TestType = testType,
                TickOverride = request.TickOverride,
                Bots = request.BotsToTest ?? new List<string>(),
            };

            _testDefinitionLoader.SaveTestDefinition(testDefinition);

            var dto = new TestDefinitionDto
            {
                TestName = testDefinition.TestName,
                GameStateFile = testDefinition.GameStateFile,
                Description = testDefinition.Description,
                BotNicknameInStateFile = testDefinition.BotNicknameInStateFile,
                ExpectedAction = testDefinition.ExpectedAction?.ToString(),
                AcceptableActions = testDefinition.AcceptableActions.Select(a => a.ToString()).ToList(),
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

    [HttpPost("direct")]
    public ActionResult<TestResultDto> RunTestDirect([FromBody] CreateTestRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.TestName))
            {
                return BadRequest("Test name is required");
            }

            if (!Enum.TryParse<TestType>(request.TestType, true, out var testType))
            {
                return BadRequest($"Invalid test type: {request.TestType}");
            }

            var testDefinition = new TestDefinition
            {
                TestName = request.TestName,
                GameStateFile = request.GameStateFile ?? "temp-state.json",
                Description = request.Description ?? $"Direct test execution for {request.TestName}",
                BotNicknameInStateFile = request.BotNicknameInState,
                ExpectedAction = request.ExpectedAction,
                AcceptableActions = request.AcceptableActions ?? new List<Marvijo.Zooscape.Bots.Common.Enums.BotAction>(),
                TestType = testType,
                TickOverride = request.TickOverride,
                Bots = request.BotsToTest ?? new List<string>(),
            };

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
                    BotResults = new List<BotResultDto>(),
                }
            );
        }
    }

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
                            BotResults = new List<BotResultDto>(),
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

        foreach (var gameDir in Directory.GetDirectories(logsDir))
        {
            var jsonFiles = Directory.GetFiles(gameDir, "*.json");
            foreach (var jsonFile in jsonFiles)
            {
                try
                {
                    string content = System.IO.File.ReadAllText(jsonFile);
                    if (content.Contains(request.BotNickname))
                    {
                        var state = JsonSerializer.Deserialize<GameState>(content);
                        if (state != null && state.Tick >= request.MinTick && state.Tick <= request.MaxTick)
                        {
                            matchingFiles.Add(jsonFile);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Could not read or parse file {File}", jsonFile);
                }
            }
        }

        var skip = (request.PageNumber - 1) * request.PageSize;
        var take = request.PageSize;
        var pagedFiles = matchingFiles.Skip(skip).Take(take).ToList();

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
                    break;

                case TestType.SingleBot:
                    if (string.IsNullOrEmpty(definition.BotNicknameInStateFile))
                    {
                        throw new InvalidOperationException($"Test {definition.TestName} is of type SingleBot but has no BotNickname specified.");
                    }
                    var bot = _botFactory.CreateBot(definition.BotNicknameInStateFile);
                    var result = ExecuteBotTest(bot, definition.BotNicknameInStateFile, gameState, testParams);
                    botResults.Add(result);
                    break;

                case TestType.MultiBotArray:
                    if (definition.Bots.Count == 0)
                    {
                        throw new InvalidOperationException($"Test {definition.TestName} has TestType.MultiBotArray but no bots specified");
                    }

                    foreach (var botType in definition.Bots)
                    {
                        var botInstance = _botFactory.CreateBot(botType);
                        var botResult = ExecuteBotTest(botInstance, botType, gameState, testParams);
                        botResults.Add(botResult);
                    }
                    break;

                case TestType.TickOverride:
                    break;

                default:
                    throw new NotSupportedException($"Test type {definition.TestType} not supported");
            }

            stopwatch.Stop();
            return new TestResultDto
            {
                TestName = definition.TestName,
                Success = botResults.All(r => r.Success),
                ErrorMessage = string.Join("; ", botResults.Where(r => !r.Success).Select(r => r.ErrorMessage)),
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                BotResults = botResults,
                GameStateFile = definition.GameStateFile,
                TestType = definition.TestType.ToString(),
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex, "Error executing test {TestName}", definition.TestName);
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

    private TestResultDto ExecuteTestDirect(TestDefinition definition, object currentGameState)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var botResults = new List<BotResultDto>();

        try
        {
            var jsonString = JsonSerializer.Serialize(currentGameState);
            var gameState = JsonSerializer.Deserialize<GameState>(jsonString);

            var testParams = new TestParams
            {
                TestName = definition.TestName,
                TestGameStateJsonPath = definition.GameStateFile,
                AcceptableActions = definition.AcceptableActions,
                BotNicknameToTest = definition.BotNicknameInStateFile,
                ExpectedAction = definition.ExpectedAction,
            };

            switch (definition.TestType)
            {
                case TestType.SingleBot:
                    var bot = _botFactory.CreateBot(definition.BotNicknameInStateFile);
                    var result = ExecuteBotTest(bot, definition.BotNicknameInStateFile, gameState, testParams);
                    botResults.Add(result);
                    break;

                case TestType.MultiBotArray:
                    foreach (var botType in definition.Bots)
                    {
                        var botInstance = _botFactory.CreateBot(botType);
                        var botResult = ExecuteBotTest(botInstance, botType, gameState, testParams);
                        botResults.Add(botResult);
                    }
                    break;

                default:
                    throw new NotSupportedException($"Test type {definition.TestType} not supported for direct execution");
            }

            stopwatch.Stop();
            return new TestResultDto
            {
                TestName = definition.TestName,
                Success = botResults.All(r => r.Success),
                ErrorMessage = string.Join("; ", botResults.Where(r => !r.Success).Select(r => r.ErrorMessage)),
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

    private BotResultDto ExecuteBotTest(object bot, string botType, GameState gameState, TestParams testParams)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var animal = gameState.Animals.FirstOrDefault(a => a.Nickname?.Equals(testParams.BotNicknameToTest, StringComparison.OrdinalIgnoreCase) == true);
            if (animal == null)
            {
                throw new InvalidOperationException($"No animal found with nickname '{testParams.BotNicknameToTest}' in the provided game state.");
            }

            var getActionMethod = bot.GetType().GetMethod("GetAction", new[] { typeof(GameState), typeof(string) });
            if (getActionMethod == null)
            {
                // Fallback for bots that may not have the overload with animalId
                getActionMethod = bot.GetType().GetMethod("GetAction", new[] { typeof(GameState) });
                if (getActionMethod == null)
                {
                    throw new InvalidOperationException($"Bot of type {bot.GetType().Name} does not have a suitable GetAction method.");
                }
            }

            var actionObject = getActionMethod.GetParameters().Length == 2
                ? getActionMethod.Invoke(bot, new object[] { gameState, animal.Id })
                : getActionMethod.Invoke(bot, new object[] { gameState });
            if (actionObject is not BotAction action)
            {
                throw new InvalidOperationException("GetAction method did not return a BotAction.");
            }

            bool success = false;
            if (testParams.ExpectedAction.HasValue)
            {
                success = action == testParams.ExpectedAction.Value;
            }
            else if (testParams.AcceptableActions.Any())
            {
                success = testParams.AcceptableActions.Contains(action);
            }

            stopwatch.Stop();
            return new BotResultDto
            {
                BotType = botType,
                Action = action.ToString(),
                Success = success,
                ErrorMessage = success ? null : $"Expected { (testParams.ExpectedAction.HasValue ? testParams.ExpectedAction.Value.ToString() : string.Join(" or ", testParams.AcceptableActions))} but got {action}",
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex, "Error executing test for bot {BotType}", botType);
            return new BotResultDto
            {
                BotType = botType,
                Success = false,
                ErrorMessage = ex.Message,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
            };
        }
    }
}
