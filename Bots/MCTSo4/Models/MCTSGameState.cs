namespace MCTSo4.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the full game state used by MCTS.
    /// TODO: Populate with map, entities, scores, power-ups, ticks.
    /// </summary>
    public class MCTSGameState
    {
        // Properties matching the server payload
        public DateTime TimeStamp { get; set; }
        public int Tick { get; set; }
        public List<Cell> Cells { get; set; } = new List<Cell>();
        public List<Animal> Animals { get; set; } = new List<Animal>();
        public List<Zookeeper> Zookeepers { get; set; } = new List<Zookeeper>();

        // Example properties
        // public int[,] Map { get; set; }
        // public IList<Position> PelletPositions { get; set; }
        // public IList<Zookeeper> Zookeepers { get; set; }
        // public Position PlayerPosition { get; set; }
        // public int CurrentTick { get; set; }

        // Creates a shallow clone for simulation
        public MCTSGameState Clone() => (MCTSGameState)MemberwiseClone();

        // Returns all legal moves from this state
        public List<Move> GetLegalMoves() => Enum.GetValues<Move>().ToList();

        // Applies a move and returns the resulting state
        public MCTSGameState Apply(Move move)
        {
            var next = Clone();
            // TODO: apply move to next state
            return next;
        }

        // Checks if this state is terminal (game over)
        public bool IsTerminal() => false; // TODO: implement terminal condition

        // Evaluates terminal or non-terminal states for simulation
        public double Evaluate() => 0.0; // TODO: return [1=win,0=loss]
    }
}
