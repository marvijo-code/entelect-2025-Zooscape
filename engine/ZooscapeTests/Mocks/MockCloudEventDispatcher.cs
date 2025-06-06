using System.Threading.Tasks;
using Zooscape.Application.Events;

namespace ZooscapeTests.Mocks;

public class MockCloudEventDispatcher() : IEventDispatcher
{
    public async Task Dispatch<TEvent>(TEvent gameEvent)
        where TEvent : class { }
}
