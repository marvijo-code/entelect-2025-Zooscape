using System.Reflection;
using System.Text.Json;
using Marvijo.Zooscape.Bots.Common.Models;
using NSubstitute;
using Serilog;

namespace FunctionalTests;

public static class BotTestHelper
{
    private static readonly string BasePath = Path.GetDirectoryName(
        Assembly.GetExecutingAssembly().Location
    )!;
    private static readonly string TestStatesPath = Path.Combine(BasePath, "TestStates");

    public static List<TestDefinition> LoadTestDefinitions()
    {
        var filePath = Path.Combine(BasePath, "test_definitions.json");
        if (!File.Exists(filePath))
        {
            return new List<TestDefinition>();
        }
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<TestDefinition>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? new List<TestDefinition>();
    }

    public static void SaveTestDefinition(TestDefinition testDefinition)
    {
        var definitions = LoadTestDefinitions();
        definitions.RemoveAll(td =>
            td.TestName == testDefinition.TestName
            && td.GameStateFile == testDefinition.GameStateFile
        ); // Remove existing if any, to update
        definitions.Add(testDefinition);
        var filePath = Path.Combine(BasePath, "test_definitions.json");
        var json = JsonSerializer.Serialize(
            definitions,
            new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true }
        );
        File.WriteAllText(filePath, json);
    }

    public static string SaveGameStateFile(string testName, string jsonContent)
    {
        if (!Directory.Exists(TestStatesPath))
        {
            Directory.CreateDirectory(TestStatesPath);
        }
        // Sanitize testName to be a valid filename
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeTestName = new string(
            testName.Where(ch => !invalidChars.Contains(ch)).ToArray()
        ).Replace(" ", "_");
        var fileName = $"{safeTestName}_{DateTime.Now:yyyyMMddHHmmssfff}.json";
        var filePath = Path.Combine(TestStatesPath, fileName);
        File.WriteAllText(filePath, jsonContent);
        return fileName; // Return the actual filename saved
    }

    internal static GameState LoadGameState(
        string testFileName,
        string v,
        Position? startingPosition
    )
    {
        throw new NotImplementedException();
    }
}
