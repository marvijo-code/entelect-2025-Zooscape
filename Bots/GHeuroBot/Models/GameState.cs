using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HeuroBot.Models;

public class GameState
{
    public DateTime TimeStamp { get; set; }
    public int Tick { get; set; }
    public List<Cell> Cells { get; set; }
    public List<Animal> Animals { get; set; }
    public List<Zookeeper> Zookeepers { get; set; }
    public double MaxTicks { get; internal set; } = 2000;

    public override string ToString() => JsonSerializer.Serialize(this);

    internal Cell GetCell(int nx, int ny)
    {
        return Cells.FirstOrDefault(x => x.X == nx && x.Y == ny);
    }
}
