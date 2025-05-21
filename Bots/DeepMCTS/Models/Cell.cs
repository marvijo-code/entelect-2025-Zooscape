using DeepMCTS.Enums; // Changed namespace for Enums

namespace DeepMCTS.Models // Changed namespace
{
    public class Cell
    {
        public int X { get; set; }
        public int Y { get; set; }
        public CellContent Content { get; set; }
    }
}
