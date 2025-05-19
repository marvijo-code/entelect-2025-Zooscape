namespace MCTSo4.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq; // Added for Linq operations
    using MCTSo4.Enums; // Added for CellContent enum
    using Serilog;

    /// <summary>
    /// Represents the full game state used by MCTS.
    /// TODO: Populate with map, entities, scores, power-ups, ticks.
    /// </summary>
    public class MCTSGameState
    {
        private static readonly ILogger GameStateLog = Log.ForContext<MCTSGameState>();

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
        public MCTSGameState Clone()
        {
            // Need a deep clone for simulation to prevent modifying shared lists
            var clonedState = (MCTSGameState)MemberwiseClone();
            clonedState.Cells = Cells
                .Select(c => new Cell
                {
                    X = c.X,
                    Y = c.Y,
                    Content = c.Content,
                })
                .ToList();
            clonedState.Animals = Animals
                .Select(a => new Animal
                {
                    Id = a.Id,
                    X = a.X,
                    Y = a.Y,
                    SpawnX = a.SpawnX,
                    SpawnY = a.SpawnY,
                    Score = a.Score,
                    CapturedCounter = a.CapturedCounter,
                    DistanceCovered = a.DistanceCovered,
                    IsViable = a.IsViable,
                })
                .ToList();
            clonedState.Zookeepers = Zookeepers
                .Select(z => new Zookeeper
                {
                    Id = z.Id,
                    X = z.X,
                    Y = z.Y,
                    SpawnX = z.SpawnX,
                    SpawnY = z.SpawnY,
                })
                .ToList();
            return clonedState;
        }

        // Returns all legal moves from this state
        public List<Move> GetLegalMoves() => Enum.GetValues<Move>().ToList();

        // Applies a move and returns the resulting state
        public MCTSGameState Apply(Move move, Guid botAnimalId)
        {
            var nextState = Clone(); // Start with a deep clone
            var botAnimal = nextState.Animals.FirstOrDefault(a => a.Id == botAnimalId);

            if (botAnimal == null)
            {
                GameStateLog.Error(
                    "Apply called for BotId {BotId}, but animal not found in state. Returning current state.",
                    botAnimalId
                );
                return nextState; // Or throw an exception
            }

            int currentX = botAnimal.X;
            int currentY = botAnimal.Y;
            int nextX = currentX;
            int nextY = currentY;

            switch (move)
            {
                case Move.Up:
                    nextY--;
                    break;
                case Move.Down:
                    nextY++;
                    break;
                case Move.Left:
                    nextX--;
                    break;
                case Move.Right:
                    nextX++;
                    break;
            }

            // Wall collision check (assuming map boundaries are handled by walls or are very large)
            bool isWallCollision = nextState.Cells.Any(c =>
                c.X == nextX && c.Y == nextY && c.Content == CellContent.Wall
            );

            if (!isWallCollision)
            {
                botAnimal.X = nextX;
                botAnimal.Y = nextY;

                // Pellet collection check
                var pelletCell = nextState.Cells.FirstOrDefault(c =>
                    c.X == nextX && c.Y == nextY && c.Content == CellContent.Pellet
                );
                if (pelletCell != null)
                {
                    botAnimal.Score++; // Assuming 1 point per pellet
                    pelletCell.Content = CellContent.Empty;
                    GameStateLog.Verbose(
                        "Bot {BotId} collected a pellet at ({X},{Y}). New score: {Score}",
                        botAnimalId,
                        nextX,
                        nextY,
                        botAnimal.Score
                    );
                }
            }
            else
            {
                GameStateLog.Verbose(
                    "Bot {BotId} move {Move} to ({X},{Y}) resulted in wall collision.",
                    botAnimalId,
                    move,
                    nextX,
                    nextY
                );
                // Animal stays in the same place if wall collision
            }

            nextState.Tick++; // Increment tick for the new state
            return nextState;
        }

        // Checks if this state is terminal (game over)
        public bool IsTerminal(
            Guid botAnimalId,
            BotParameters parameters,
            int currentTickInSim,
            int maxSimDepth
        )
        {
            var botAnimal = Animals.FirstOrDefault(a => a.Id == botAnimalId);
            if (botAnimal == null)
                return true; // Should not happen if Apply worked

            // Caught by Zookeeper
            foreach (var zk in Zookeepers)
            {
                if (botAnimal.X == zk.X && botAnimal.Y == zk.Y)
                {
                    GameStateLog.Debug(
                        "Bot {BotId} is terminal: Caught by zookeeper at ({X},{Y}) in sim tick {SimTick}",
                        botAnimalId,
                        zk.X,
                        zk.Y,
                        currentTickInSim
                    );
                    return true;
                }
            }

            // Simulation depth reached
            if (currentTickInSim >= maxSimDepth)
            {
                GameStateLog.Verbose(
                    "Bot {BotId} is terminal: Simulation depth {SimTick}/{MaxDepth} reached.",
                    botAnimalId,
                    currentTickInSim,
                    maxSimDepth
                );
                return true;
            }

            // TODO: Add other game-specific terminal conditions (e.g., all pellets collected, specific game objectives met)
            // For now, these are the main ones for simulation.
            return false;
        }

        // Evaluates terminal or non-terminal states for simulation
        public double Evaluate(Guid botAnimalId, BotParameters parameters)
        {
            var botAnimal = Animals.FirstOrDefault(a => a.Id == botAnimalId);
            if (botAnimal == null)
                return -100000; // Heavily penalize if bot animal somehow disappears

            double evalScore = 0.0;

            // Base score from animal's collected points
            evalScore += botAnimal.Score * parameters.Weight_PelletValue;

            bool wasCaught = false;
            // Zookeeper threat / being caught
            foreach (var zk in Zookeepers)
            {
                if (botAnimal.X == zk.X && botAnimal.Y == zk.Y)
                {
                    evalScore -= 100000; // Massive penalty for being caught
                    wasCaught = true;
                    break;
                }
                int distToZk = Math.Abs(botAnimal.X - zk.X) + Math.Abs(botAnimal.Y - zk.Y);
                if (distToZk < 5 && distToZk > 0) // Example threshold for proximity threat
                {
                    // Weight_ZkThreat is negative, so this subtracts from score
                    evalScore += parameters.Weight_ZkThreat / distToZk;
                }
            }
            if (wasCaught)
                GameStateLog.Debug(
                    "Evaluate for Bot {BotId}: Caught by zookeeper. Score: {Score}",
                    botAnimalId,
                    evalScore
                );

            // Pellet attraction: Encourage moving towards the closest pellet
            double minDistanceToPellet = double.MaxValue;
            Cell closestPellet = null;
            foreach (var cell in Cells)
            {
                if (cell.Content == CellContent.Pellet)
                {
                    int dist = Math.Abs(botAnimal.X - cell.X) + Math.Abs(botAnimal.Y - cell.Y);
                    if (dist < minDistanceToPellet)
                    {
                        minDistanceToPellet = dist;
                        closestPellet = cell;
                    }
                }
            }

            if (closestPellet != null && minDistanceToPellet > 0)
            {
                // Add score based on proximity to pellet, ensure Weight_PelletValue is positive for attraction
                evalScore += (parameters.Weight_PelletValue * 2) / (minDistanceToPellet + 1); // Boost pellet attraction a bit more, add 1 to avoid div by zero
            }
            else if (closestPellet == null && !wasCaught)
            {
                // No pellets left and not caught? Maybe this is a win or good state.
                evalScore += 5000; // Bonus for clearing pellets (if that's a goal)
            }

            // TODO: Add evaluation for parameters.Weight_EscapeProgress, parameters.Weight_PowerUp
            // This would require knowing how these are represented in the game state (e.g., exit points, power-up items)

            GameStateLog.Verbose(
                "Evaluate for Bot {BotId} at ({X},{Y}), Tick {Tick}, SimScore {Score}. Animal Score: {AnimalScore}",
                botAnimalId,
                botAnimal.X,
                botAnimal.Y,
                Tick,
                evalScore,
                botAnimal.Score
            );
            return evalScore;
        }
    }
}
