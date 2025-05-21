using DeepMCTS.Enums; // Added for BotAction

namespace DeepMCTS.Models // Changed namespace
{
    public class BotCommand
    {
        public BotAction Action { get; set; } // Changed Move to BotAction
    }
}
