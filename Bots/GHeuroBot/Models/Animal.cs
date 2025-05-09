using System;
using HeuroBot.Enums;

namespace HeuroBot.Models;

public class Animal
{
    public Guid Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int SpawnX { get; set; }
    public int SpawnY { get; set; }
    public int Score { get; set; }
    public int CapturedCounter { get; set; }
    public int DistanceCovered { get; set; }
    public bool IsViable { get; set; }
    public bool IsInCage { get; set; }
    public decimal TicksSinceLastCaught { get; internal set; }
    public BotAction PreviousAction { get; internal set; }
    public int TimeSpentOnSpawn { get; internal set; }
}
