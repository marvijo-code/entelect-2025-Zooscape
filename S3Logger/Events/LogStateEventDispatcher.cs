using System.Text.Json;
using S3Logger.Utilities;
using Zooscape.Application.Events;
using Zooscape.Infrastructure.SignalRHub.Models;

namespace S3Logger.Events;

public class LogStateEventDispatcher(IStreamingFileLogger logger) : IEventDispatcher
{
    public async Task Dispatch<TEvent>(TEvent gameEvent)
        where TEvent : class
    {
        if (gameEvent is GameStateEvent gameStateEvent)
        {
            var state = new GameState(gameStateEvent.GameState);
            logger.LogState(JsonSerializer.Serialize(state));
        }
        else if (gameEvent is CloseAndFlushLogsEvent)
        {
            await logger.CloseAndFlushAsync();
            await S3.UploadLogs();
        }
    }
}
