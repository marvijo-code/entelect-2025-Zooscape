using Marvijo.Zooscape.Bots.Common.Enums; // Added for BotAction

namespace Marvijo.Zooscape.Bots.Common.Models; // Changed namespace

public class BotCommand
{
    public BotAction Action { get; set; } // Changed Move to BotAction
}
