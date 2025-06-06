using System;
using System.Threading.Tasks;
using Zooscape.Application.Events;

namespace ZooscapeTests.Mocks;

public class MockSignalREventDispatcher(Action<int> action) : IEventDispatcher
{
    private int tickCounter = 0;

    public async Task Dispatch<TEvent>(TEvent gameEvent)
        where TEvent : class
    {
        if (gameEvent is GameStateEvent)
        {
            action(++tickCounter);
        }
    }
}
