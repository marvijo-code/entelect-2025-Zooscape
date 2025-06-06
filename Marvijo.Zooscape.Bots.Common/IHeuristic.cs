#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace Marvijo.Zooscape.Bots.Common
{
    public interface IHeuristic
    {
        string Name { get; }
        decimal CalculateRawScore(GameState state, Animal me, BotAction move, ILogger? logger);
    }
}
