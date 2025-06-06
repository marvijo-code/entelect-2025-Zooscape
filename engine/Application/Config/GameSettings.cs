﻿using System;
using System.Collections.Generic;
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

    [Description("Score multiplier as animal collects consecutive pellets")]
    public ScoreStreak ScoreStreak { get; set; } = new ScoreStreak { };

    [Description("Additional zookeepers config")]
    public Zookeepers Zookeepers { get; set; } = new Zookeepers { };

    [Description("Powerup parameters")]
    public PowerUps PowerUps { get; set; } = new PowerUps { };

    [Description("Obstacle parameters")]
    public Obstacles Obstacles { get; set; } = new Obstacles() { };

    [Description("Pellet respawn parameters")]
    public PelletRespawn PelletRespawn { get; set; } = new PelletRespawn();

    [Description("Seed used for random number generation")]
    public int Seed { get; set; } = new Random().Next();
}

public class ScoreStreak
{
    [Description(
        "Amount by which score streak multiplier is incremented for each pellet collected"
    )]
    public double MultiplierGrowthFactor { get; set; }

    [Description("Maximum value that score multiplier is allowed to grow to")]
    public double Max { get; set; }

    [Description("Number of missed pellets allowed before resetting score streak")]
    public int ResetGrace { get; set; }
}

public class Zookeepers
{
    [Description("Parameters for randomisation of new zookeeper spawning")]
    public SpawnIntervals SpawnInterval { get; set; } = new SpawnIntervals { };

    [Description("Maximum number of zookeepers to be added to the game")]
    public int Max { get; set; }
}

public class PowerUps
{
    [Description("Parameters for randomisation of new powerup spawning")]
    public SpawnIntervals SpawnInterval { get; set; } = new SpawnIntervals { };

    [Description("The minimum distance away from players that power ups can spawn")]
    public int DistanceFromPlayers { get; set; }

    [Description("The minimum distance from other power ups that power ups can spawn")]
    public int DistanceFromOtherPowerUps { get; set; }

    [Description("Parameters for each powerup type")]
    public Dictionary<string, PowerUpParameters> Types { get; set; } = [];
}

public class Obstacles
{
    [Description("Parameters for randomisation of new obstacle spawning")]
    public SpawnIntervals SpawnInterval { get; set; } = new SpawnIntervals { };

    [Description("The minimum distance away from players that obstacles can spawn")]
    public int DistanceFromPlayers { get; set; }

    [Description("The minimum distance away from player spawn points that obstacles can spawn")]
    public int DistanceFromPlayerSpawnPoints { get; set; }

    [Description("The minimum distance from other obstacles that obstacles can spawn")]
    public int DistanceFromOtherObstacles { get; set; }
}

public class PelletRespawn
{
    [Description("Parameters for randomisation of pellet respawning")]
    public SpawnIntervals SpawnInterval { get; set; } = new SpawnIntervals { };
}

public class SpawnIntervals
{
    [Description("Mean interval between spawns")]
    public int Mean { get; set; }

    [Description("Standard deviation for normal distribution of spawn interval")]
    public double StdDev { get; set; }

    [Description("Minimum spawn interval")]
    public int Min { get; set; }

    [Description("Maximum spawn interval")]
    public int Max { get; set; }
}

public class PowerUpParameters
{
    [Description(
        "Likelihood of powerup selected for next spawn. Higher value = more likely to spawn."
    )]
    public int RarityWeight { get; set; }

    [Description("Powerup specific value (radius, multiplier etc.)")]
    public int Value { get; set; }

    [Description("Number of game ticks that powerup remains active")]
    public int Duration { get; set; }
}
