using NETCoreBot.Models;

namespace NETCoreBot.Services
{
    public class BotService
    {
        private Guid _botId;

        public void SetBotId(Guid botId)
        {
            _botId = botId;
        }

        public Guid GetBotId()
        {
            return _botId;
        }

        public BotCommand ProcessState(GameState gameStateDTO)
        {
            //Add your custom bot logic here
            return new BotCommand { Action = Enums.BotAction.Up };
        }
    }
}
