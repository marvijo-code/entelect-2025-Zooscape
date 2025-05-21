using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Models;
using NSubstitute;
using Serilog;

// Add using statements for each of your bot namespaces here, e.g.:
// using Bots.RulesBot;
// using Bots.GatherNearBot;

namespace FunctionalTests;

public static class BotFactory
{
    public static IEnumerable<IBot<GameState>> GetAllBots(ILogger? logger = null)
    {
        return null;
    }
}
