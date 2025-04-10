namespace S3Logger;

public interface IStreamingFileLogger
{
    void LogState(object state);

    void CloseAndFlush();
}
