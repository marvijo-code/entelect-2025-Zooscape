using System;
using System.IO;
using System.Text.Json;
using S3Logger.Utilities;
using Zooscape.Application.Events;
using Zooscape.Infrastructure.SignalRHub.Models;

namespace S3Logger.Events;

public class LogStateEventDispatcher : IEventDispatcher
{
    private readonly IStreamingFileLogger _logger;

    private readonly string _matchDir;

    public LogStateEventDispatcher(IStreamingFileLogger logger)
    {
        _logger = logger;
        _matchDir =
            Environment.GetEnvironmentVariable("LOG_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "logs");
    }

    public async Task Dispatch<TEvent>(TEvent gameEvent)
        where TEvent : class
    {
        if (gameEvent is GameStateEvent gameStateEvent)
        {
            var state = new GameState(gameStateEvent.GameState);
            var json = JsonSerializer.Serialize(state);
            _logger.LogState(json);

            // Save to per-tick file
            int tick = 0;
            var tickProp = state.GetType().GetProperty("CurrentTick");
            if (tickProp != null && tickProp.GetValue(state) is int t)
                tick = t;
            var filePath = Path.Combine(_matchDir, $"tick_{tick}.json");
            File.WriteAllText(filePath, json);
        }
        else if (gameEvent is CloseAndFlushLogsEvent)
        {
            _logger.CloseAndFlush();
        }
    }
}
