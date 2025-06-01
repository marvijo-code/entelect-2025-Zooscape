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
    /// Load all test definitions from JSON files in the TestDefinitions directory
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

        var jsonFiles = Directory
            .GetFiles(testDefinitionsPath, "*.json", SearchOption.AllDirectories)
            .Where(f =>
                !Path.GetFileName(f).Equals("TestSchema.json", StringComparison.OrdinalIgnoreCase)
            )
            .ToArray();
        _logger.Information("Found {Count} JSON test definition files", jsonFiles.Length);

        foreach (var filePath in jsonFiles)
        {
            try
            {
                var definitions = LoadTestDefinitionsFromFile(filePath);
                testDefinitions.AddRange(definitions);
                _logger.Information(
                    "Loaded {Count} test definitions from {FileName}",
                    definitions.Count,
                    Path.GetFileName(filePath)
                );
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load test definitions from {FilePath}", filePath);
            }
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
