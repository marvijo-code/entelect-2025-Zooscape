using System;
using System.Collections.Generic;

public enum CellContent
{
    Empty = 0,
    Wall = 1,
    Pellet = 2,
    ZookeeperSpawn = 3,
    AnimalSpawn = 4,
    PowerPellet = 5,
    ChameleonCloak = 6,
    Scavenger = 7,
    BigMooseJuice = 8,
}

public enum PowerUpType
{
    PowerPellet = 0,
    ChameleonCloak = 1,
    Scavenger = 2,
    BigMooseJuice = 3,
}

public class TickState
{
    public List<GameState> WorldStates { get; set; }
}

public class GameState
{
    public DateTime TimeStamp { get; set; }
    public int Tick { get; set; }
    public List<Cell> Cells { get; set; }
    public List<Animal> Animals { get; set; }
    public List<Zookeeper> Zookeepers { get; set; }
}

public class Cell
{
    public int X { get; set; }
    public int Y { get; set; }
    public CellContent Content { get; set; }
}

public class Zookeeper
{
    public Guid Id { get; set; }
    public string NickName { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int SpawnX { get; set; }
    public int SpawnY { get; set; }
}

public class Animal
{
    public Guid Id { get; set; }
    public string NickName { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int SpawnX { get; set; }
    public int SpawnY { get; set; }
    public int Score { get; set; }
    public int CapturedCounter { get; set; }
    public int DistanceCovered { get; set; }
    public bool IsViable { get; set; }
    public PowerUpType? HeldPowerUp { get; set; }
    public ActivePowerUp? ActivePowerUp { get; set; }
}

public class ActivePowerUp
{
    public double Value { get; set; }
    public int TicksRemaining { get; set; }
    public PowerUpType Type { get; set; }
}
