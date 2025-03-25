using System.ComponentModel;

namespace Zooscape.Application.Config;

public class GameSettings
{
    [Description("Path to world map")]
    public string WorldMap { get; set; } = "file:StarterWorlds/World1.txt";

    [Description("Maximum wait time (in seconds) for game ready conditions to be met")]
    public int StartGameTimeout { get; set; } = 30;

    [Description("Interval (in milliseconds) of game loop ticks")]
    public int TickDuration { get; set; } = 100;

    [Description("Maximum number of ticks before game ends")]
    public int MaxTicks { get; set; } = 1000;

    [Description("Number of bots required before game starts")]
    public int NumberOfBots { get; set; } = 4;

    [Description("Number of commands that each bot can queue up")]
    public int CommandQueueSize { get; set; } = 10;

    [Description(
        "Amount of ticks that need to elapse before the zookeeper can recalculate its target"
    )]
    public int TicksBetweenZookeeperRetarget { get; set; } = 20;

    [Description("Points added to an animal's score when collecting a pellet")]
    public int PointsPerPellet { get; set; } = 1;

    [Description("Percentage of an animal's score that is lost when it is captured")]
    public int ScoreLossPercentage { get; set; } = 10;
}
