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
    private readonly StreamWriter _json;
    private bool _first = true;

    public LogStateEventDispatcher(IStreamingFileLogger logger)
    {
        _logger = logger;
        var matchDir =
            Environment.GetEnvironmentVariable("LOG_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "logs");
        var path = Path.Combine(matchDir, "gameState.json");
        _json = new StreamWriter(path);
        _json.WriteLine("[");
    }

    public async Task Dispatch<TEvent>(TEvent gameEvent)
        where TEvent : class
    {
        if (gameEvent is GameStateEvent gameStateEvent)
        {
            var state = new GameState(gameStateEvent.GameState);
            var json = JsonSerializer.Serialize(state);
            _logger.LogState(json);

            if (!_first)
                _json.WriteLine(",");
            else
                _first = false;
            _json.Write(json);
        }
        else if (gameEvent is CloseAndFlushLogsEvent)
        {
            _logger.CloseAndFlush();

            _json.WriteLine();
            _json.WriteLine("]");
            await _json.FlushAsync();
            _json.Close();
        }
    }
}
