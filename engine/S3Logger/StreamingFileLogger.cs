using System.IO.Compression;
using Serilog;
using Serilog.Sinks.File.GZip;

namespace S3Logger;

public class StreamingFileLogger : IStreamingFileLogger
{
    private readonly ILogger _logger;

    public StreamingFileLogger(string logDir, string logFileName)
    {
        string logFilePath = Path.Combine(logDir, $"{logFileName}.gz");
        Log.Information($"Logging output to: {logFilePath}");

        _logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                logFilePath,
                outputTemplate: "{Message:lj}{NewLine}",
                hooks: new GZipHooks(compressionLevel: CompressionLevel.Optimal)
            )
            .CreateLogger();
    }

    public void LogState(object state)
    {
        _logger.Information("{@state}", state);
    }

    public void CloseAndFlush()
    {
        ((IDisposable)_logger).Dispose();
    }
}
