namespace Marvijo.Zooscape.Bots.Common.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // using Serilog; // Serilog might not be needed directly in the model

    /// <summary>
    /// Represents the full game state received from the server.
    /// </summary>
    public class GameState
    {
        // private static readonly ILogger GameStateLog = Log.ForContext<GameState>(); // Removed Serilog instance

        // Properties matching the server payload
        public DateTime TimeStamp { get; set; }
        public int Tick { get; set; } // Renamed back from CurrentTick to Tick
        public List<Cell> Cells { get; set; } = new List<Cell>();
        public List<Animal> Animals { get; set; } = new List<Animal>(); // MyAnimal will be derived from this
        public List<Zookeeper> Zookeepers { get; set; } = new List<Zookeeper>();
        public Guid MyAnimalId { get; set; } // Added to identify which animal in the list is ours

        // Convenience property to get MyAnimal
        // The new MonteCarlo logic expects state.MyAnimal directly.
        public Animal? MyAnimal => Animals?.FirstOrDefault(a => a.Id == MyAnimalId);

        // Constructor (optional, can be default)
        public GameState()
        {
            // Initialize lists if not done by deserializer to prevent null references
            Cells = Cells ?? new List<Cell>();
            Animals = Animals ?? new List<Animal>();
            Zookeepers = Zookeepers ?? new List<Zookeeper>();
        }

        // The new MonteCarlo simulation does not use this GameState's Clone, Apply, IsTerminal, Evaluate methods.
        // It has its own internal simulation logic and state handling (pellet bitboards, etc.).
        // So, those MCTS-specific methods from MCTSGameState are removed.
    }
}
