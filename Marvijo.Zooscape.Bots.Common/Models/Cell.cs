using Marvijo.Zooscape.Bots.Common.Enums;

namespace Marvijo.Zooscape.Bots.Common.Models; // Changed namespace

public class Cell
{
    public int X { get; set; }
    public int Y { get; set; }
    public CellContent Content { get; set; }
}
