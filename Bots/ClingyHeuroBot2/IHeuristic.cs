#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using Marvijo.Zooscape.Bots.Common.Enums; // Corrected using
using Marvijo.Zooscape.Bots.Common.Models; // Corrected using

namespace ClingyHeuroBot2
{
    public interface IHeuristic
    {
        string Name { get; }
        decimal CalculateRawScore(GameState state, Animal me, BotAction move);
    }
}
