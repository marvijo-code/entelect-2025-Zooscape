using System.Threading.Tasks;
using Zooscape.Application.Events;

namespace ZooscapeTests.Mocks;

public class MockLogStateEventDisptcher : IEventDispatcher
{
    public Task Dispatch<TEvent>(TEvent gameEvent)
        where TEvent : class
    {
        return Task.CompletedTask;
    }
}
