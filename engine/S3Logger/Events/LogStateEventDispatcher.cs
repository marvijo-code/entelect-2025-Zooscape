using System;
using System.IO;
using System.Text.Json;

using Zooscape.Application.Events;
using Zooscape.Infrastructure.SignalRHub.Models;
using Zooscape.Infrastructure.S3Logger;

namespace Zooscape.Infrastructure.S3Logger.Events;

public class LogStateEventDispatcher : IEventDispatcher
{
    private readonly IStreamingFileLogger _logger;

    private readonly string _matchDir;
    private readonly string _baseLogDir = @"C:\dev\2025-Zooscape\logs";

    public LogStateEventDispatcher(IStreamingFileLogger logger)
    {
        _logger = logger;

        Console.WriteLine($"Base log directory: {_baseLogDir}");
        // Create a unique directory for the current game session using date and time
        var gameSessionDirName = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _matchDir = Path.Combine(_baseLogDir, gameSessionDirName);

        // Ensure the directory exists
        Directory.CreateDirectory(_matchDir);
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
            var tickProp = state.GetType().GetProperty("Tick");
            if (tickProp != null && tickProp.GetValue(state) is int t)
                tick = t;
            var filePath = Path.Combine(_matchDir, $"{tick}.json");
            File.WriteAllText(filePath, json);
        }
        else if (gameEvent is CloseAndFlushLogsEvent)
        {
            _logger.CloseAndFlush();
        }
    }
}
