using ClingyHeuroBot2Service = HeuroBot.Services.HeuroBotService;
using ClingyHeuroBotService = HeuroBotV2.Services.HeuroBotService;

namespace FunctionalTests.Services;

/// <summary>
/// Factory for creating bot instances based on string names
/// </summary>
public class BotFactory
{
    /// <summary>
    /// Create a bot instance based on the bot type name
    /// </summary>
    public object CreateBot(string botTypeName)
    {
        return botTypeName switch
        {
            "ClingyHeuroBot2" => new ClingyHeuroBot2Service(),
            "ClingyHeuroBot" => new ClingyHeuroBotService(),
            _ => throw new ArgumentException(
                $"Unknown bot type: {botTypeName}",
                nameof(botTypeName)
            ),
        };
    }

    /// <summary>
    /// Get all available bot types
    /// </summary>
    public List<string> GetAvailableBotTypes()
    {
        return ["ClingyHeuroBot2", "ClingyHeuroBot"];
    }

    /// <summary>
    /// Create multiple bot instances based on a list of bot type names
    /// </summary>
    public object[] CreateBots(List<string> botTypeNames)
    {
        return botTypeNames.Select(CreateBot).ToArray();
    }
}
