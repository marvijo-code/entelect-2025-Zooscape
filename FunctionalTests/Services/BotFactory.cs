using ClingyHeuroBot2Service = ClingyHeuroBot2.Services.HeuroBotService;
using ClingyHeuroBotService = HeuroBotV2.Services.HeuroBotService;
using StaticHeuroService = StaticHeuro.Services.HeuroBotService;
// using DeepMCTSService = DeepMCTS.Services.BotService;
// using MCTSo4Service = MCTSo4.Services.MCTSo4Logic;

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
        object bot = botTypeName switch
        {
            "ClingyHeuroBot2" => new ClingyHeuroBot2Service(),
            "ClingyHeuroBot" => new ClingyHeuroBotService(),
            "StaticHeuro" => new StaticHeuroService(),
            // "DeepMCTS" => new DeepMCTSService(),
            // "MCTSo4" => new MCTSo4Service(),
            _ => throw new ArgumentException($"Unknown bot type: {botTypeName}", nameof(botTypeName)),
        };

        // Enable heuristic logging for supported bots
        if (bot is StaticHeuroService staticHeuroBot)
        {
            staticHeuroBot.LogHeuristicScores = true;
        }
        else if (bot is ClingyHeuroBot2Service clingyHeuroBot2)
        {
            clingyHeuroBot2.LogHeuristicScores = true;
        }

        return bot;
    }

    /// <summary>
    /// Get all available bot types
    /// </summary>
    public List<string> GetAvailableBotTypes()
    {
        return ["ClingyHeuroBot2", "ClingyHeuroBot", "StaticHeuro"]; // , "DeepMCTS", "MCTSo4"];
    }

    /// <summary>
    /// Create multiple bot instances based on a list of bot type names
    /// </summary>
    public object[] CreateBots(List<string> botTypeNames)
    {
        return botTypeNames.Select(CreateBot).ToArray();
    }
}
