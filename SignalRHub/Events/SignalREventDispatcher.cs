using Microsoft.AspNetCore.SignalR;
using Zooscape.Application.Events;
using Zooscape.Infrastructure.SignalRHub.Hubs;
using Zooscape.Infrastructure.SignalRHub.Messages;
using Zooscape.Infrastructure.SignalRHub.Models;

namespace Zooscape.Infrastructure.SignalRHub.Events;

public class SignalREventDispatcher(IHubContext<BotHub> hubContext) : IEventDispatcher
{
    public async Task Dispatch<TEvent>(TEvent gameEvent)
        where TEvent : class
    {
        if (gameEvent is GameStateEvent gameStateEvent)
        {
            var payload = new GameState(gameStateEvent.GameState);
            await hubContext.Clients.All.SendAsync(OutgoingMessages.GameState, payload);
        }
    }
}
