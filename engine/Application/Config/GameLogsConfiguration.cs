namespace Zooscape.Application.Config;

public class GameLogsConfiguration
{
    public bool PushLogsToS3 { get; set; }
    public bool FullLogsEnabled { get; set; }
    public bool DiffLogsEnabled { get; set; } = true;
}
