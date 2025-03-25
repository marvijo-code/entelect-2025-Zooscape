using System.IO.Compression;
using Serilog;
using Serilog.Sinks.File.GZip;

namespace S3Logger;

public class StreamingFileLogger : IStreamingFileLogger
{
    private readonly ILogger _logger;

    public StreamingFileLogger(string logFileName)
    {
        var logDir = Environment.GetEnvironmentVariable("LOG_DIR");
        if (string.IsNullOrWhiteSpace(logDir))
        {
            throw new Exception("No LOG_DIR environment variable defined.");
        }

        string logFilePath = Path.Combine(logDir, $"{logFileName}.gz");

        _logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Information()
            .WriteTo.File(
                logFilePath,
                outputTemplate: "{Message:lj}{NewLine}",
                hooks: new GZipHooks(compressionLevel: CompressionLevel.Fastest)
            )
            .CreateLogger();
    }

    public void LogState(object state)
    {
        _logger.Information("{@state}", state);
    }

    public async Task CloseAndFlushAsync()
    {
        await Log.CloseAndFlushAsync();
    }
}
