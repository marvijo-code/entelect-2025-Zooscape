﻿using System.Text.Json;

namespace PlayableBot.Models;

public class GameState
{
    public DateTime TimeStamp { get; set; }
    public int Tick { get; set; }
    public List<Cell> Cells { get; set; }
    public List<Animal> Animals { get; set; }
    public List<Zookeeper> Zookeepers { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}
