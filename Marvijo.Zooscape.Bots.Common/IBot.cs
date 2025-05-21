using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace Marvijo.Zooscape.Bots.Common;

public interface IBot<T>
{
    Guid BotId { get; set; }

    BotAction GetAction(GameState gameState);
}
