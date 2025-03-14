namespace S3Logger;

public interface IStreamingFileLogger
{
    void LogState(object state);

    Task CloseAndFlushAsync();
}
