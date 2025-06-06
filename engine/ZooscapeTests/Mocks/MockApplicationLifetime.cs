using System.Threading;
using Microsoft.Extensions.Hosting;

namespace ZooscapeTests.Mocks;

public class MockApplicationLifetime : IHostApplicationLifetime
{
    public void StopApplication() { }

    public CancellationToken ApplicationStarted { get; }
    public CancellationToken ApplicationStopping { get; }
    public CancellationToken ApplicationStopped { get; }
}
