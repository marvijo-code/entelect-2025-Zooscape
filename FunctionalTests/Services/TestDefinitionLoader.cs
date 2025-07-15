using System.Reflection;
using System.Text.Json;
using FunctionalTests.Models;
using Marvijo.Zooscape.Bots.Common.Enums;
using Serilog;

namespace FunctionalTests.Services;

/// <summary>
/// Service for loading test definitions from JSON files
/// </summary>
public class TestDefinitionLoader
{
    private readonly Serilog.ILogger _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public TestDefinitionLoader(Serilog.ILogger logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            Converters = { new BotActionConverter(), new TestTypeConverter() },
        };
    }

    /// <summary>
    /// Load all test definitions from the consolidated JSON file
    /// </summary>
    public List<TestDefinition> LoadAllTestDefinitions()
    {
        var testDefinitions = new List<TestDefinition>();
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
        var testDefinitionsPath = Path.Combine(assemblyDirectory!, "TestDefinitions");

        if (!Directory.Exists(testDefinitionsPath))
        {
            _logger.Warning("TestDefinitions directory not found at: {Path}", testDefinitionsPath);
            return testDefinitions;
        }

        var consolidatedTestFile = Path.Combine(testDefinitionsPath, "ConsolidatedTests.json");
        
        if (!File.Exists(consolidatedTestFile))
        {
            _logger.Warning("ConsolidatedTests.json not found at: {Path}", consolidatedTestFile);
            return testDefinitions;
        }

        try
        {
            var definitions = LoadTestDefinitionsFromFile(consolidatedTestFile);
            testDefinitions.AddRange(definitions);
            _logger.Information(
                "Loaded {Count} test definitions from ConsolidatedTests.json",
                definitions.Count
            );
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load test definitions from ConsolidatedTests.json");
        }

        _logger.Information("Total test definitions loaded: {Count}", testDefinitions.Count);
        return testDefinitions;
    }

    /// <summary>
    /// Load test definitions from a specific JSON file
    /// </summary>
    public List<TestDefinition> LoadTestDefinitionsFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Test definition file not found: {filePath}");
        }

        var jsonContent = File.ReadAllText(filePath);

        // Try to deserialize as array first, then as single object
        try
        {
            var definitions = JsonSerializer.Deserialize<List<TestDefinition>>(
                jsonContent,
                _jsonOptions
            );
            return definitions ?? [];
        }
        catch (JsonException)
        {
            // Try as single object
            try
            {
                var definition = JsonSerializer.Deserialize<TestDefinition>(
                    jsonContent,
                    _jsonOptions
                );
                return definition != null ? [definition] : [];
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to deserialize test definitions from {filePath}: {ex.Message}",
                    ex
                );
            }
        }
    }

    /// <summary>
    /// Convert TestDefinition to TestParams for compatibility with existing test framework
    /// </summary>
    public TestParams ConvertToTestParams(TestDefinition definition)
    {
        return new TestParams
        {
            TestName = definition.TestName,
            TestGameStateJsonPath = definition.GameStateFile,
            TestDescription = definition.Description ?? "",
            BotNicknameToTest = definition.BotNickname,
            ExpectedAction = definition.ExpectedAction,
            AcceptableActions = definition.AcceptableActions,
            TickOverride = definition.TickOverride,
        };
    }

    /// <summary>
    /// Save a new test definition to the ConsolidatedTests.json file
    /// </summary>
    public void SaveTestDefinition(TestDefinition testDefinition)
    {
        // Save to source directory instead of bin directory so it can be checked into version control
        var currentDirectory = Directory.GetCurrentDirectory();
        var testDefinitionsPath = Path.Combine(currentDirectory, "TestDefinitions");
        var consolidatedTestsFile = Path.Combine(testDefinitionsPath, "ConsolidatedTests.json");

        // Ensure TestDefinitions directory exists
        if (!Directory.Exists(testDefinitionsPath))
        {
            Directory.CreateDirectory(testDefinitionsPath);
            _logger.Information(
                "Created TestDefinitions directory at: {Path}",
                testDefinitionsPath
            );
        }

        // Load existing consolidated tests or create new list
        var existingTests = new List<TestDefinition>();
        if (File.Exists(consolidatedTestsFile))
        {
            try
            {
                var existingContent = File.ReadAllText(consolidatedTestsFile);
                if (!string.IsNullOrWhiteSpace(existingContent))
                {
                    var existing = JsonSerializer.Deserialize<List<TestDefinition>>(
                        existingContent,
                        _jsonOptions
                    );
                    existingTests = existing ?? new List<TestDefinition>();
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to load existing consolidated tests, starting fresh");
                existingTests = new List<TestDefinition>();
            }
        }

        // Check if test with same name already exists
        var existingIndex = existingTests.FindIndex(t =>
            t.TestName.Equals(testDefinition.TestName, StringComparison.OrdinalIgnoreCase)
        );
        if (existingIndex >= 0)
        {
            // Update existing test
            existingTests[existingIndex] = testDefinition;
            _logger.Information(
                "Updated existing test definition: {TestName}",
                testDefinition.TestName
            );
        }
        else
        {
            // Add new test
            existingTests.Add(testDefinition);
            _logger.Information("Added new test definition: {TestName}", testDefinition.TestName);
        }

        // Save back to file
        var jsonContent = JsonSerializer.Serialize(
            existingTests,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new BotActionConverter(), new TestTypeConverter() },
            }
        );

        File.WriteAllText(consolidatedTestsFile, jsonContent);
        _logger.Information("Saved test definitions to: {FilePath}", consolidatedTestsFile);
    }
}

/// <summary>
/// Custom JSON converter for BotAction enum
/// </summary>
public class BotActionConverter : System.Text.Json.Serialization.JsonConverter<BotAction>
{
    public override BotAction Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var value = reader.GetString();
        return value?.ToLowerInvariant() switch
        {
            "up" => BotAction.Up,
            "down" => BotAction.Down,
            "left" => BotAction.Left,
            "right" => BotAction.Right,
            _ => throw new JsonException($"Invalid BotAction value: {value}"),
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        BotAction value,
        JsonSerializerOptions options
    )
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// Custom JSON converter for TestType enum
/// </summary>
public class TestTypeConverter : System.Text.Json.Serialization.JsonConverter<TestType>
{
    public override TestType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var value = reader.GetString();
        return value?.ToLowerInvariant() switch
        {
            "singlebot" => TestType.SingleBot,
            "multibotarray" => TestType.MultiBotArray,
            "gamestateload" => TestType.GameStateLoad,
            "tickoverride" => TestType.TickOverride,
            _ => throw new JsonException($"Invalid TestType value: {value}"),
        };
    }

    public override void Write(Utf8JsonWriter writer, TestType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
