namespace MCTSo4.Models;

using System;
using System.Collections.Generic;
using System.Linq; // Added for Linq operations
using Marvijo.Zooscape.Bots.Common.Enums;
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

    // Helper to get cell, assuming Cells list is comprehensive and represents the grid
    private Cell? GetCell(int x, int y) => Cells.FirstOrDefault(c => c.X == x && c.Y == y);

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
        // Deep clone animal properties including new power-up fields
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
                HeldPowerUp = a.HeldPowerUp,
                ActivePowerUpEffect = a.ActivePowerUpEffect,
                PowerUpDurationTicks = a.PowerUpDurationTicks,
                ScoreStreak = a.ScoreStreak,
            })
            .ToList();
        return clonedState;
    }

    // Returns all legal moves from this state
    public List<Move> GetLegalMoves(Guid botAnimalId)
    {
        var botAnimal = Animals.FirstOrDefault(a => a.Id == botAnimalId);
        var legalMoves = new List<Move> { Move.Up, Move.Down, Move.Left, Move.Right };

        if (
            botAnimal?.HeldPowerUp != null
            && botAnimal.HeldPowerUp.Value != CellContent.PowerPellet
        ) // PowerPellet is passive
        {
            legalMoves.Add(Move.UseItem);
        }
        return legalMoves;
    }

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

        bool collectedPelletThisTurn = false;

        if (move == Move.UseItem)
        {
            if (botAnimal.HeldPowerUp.HasValue)
            {
                GameStateLog.Debug(
                    "Bot {BotId} uses {PowerUp}",
                    botAnimalId,
                    botAnimal.HeldPowerUp.Value
                );
                switch (botAnimal.HeldPowerUp.Value)
                {
                    case CellContent.ChameleonCloak:
                        botAnimal.ActivePowerUpEffect = ActivePowerUpType.ChameleonCloak;
                        botAnimal.PowerUpDurationTicks = 20;
                        break;
                    case CellContent.Scavenger:
                        botAnimal.ActivePowerUpEffect = ActivePowerUpType.Scavenger;
                        botAnimal.PowerUpDurationTicks = 5;
                        // Scavenger effect: collect pellets in 11x11 area
                        for (int sx = botAnimal.X - 5; sx <= botAnimal.X + 5; sx++)
                        {
                            for (int sy = botAnimal.Y - 5; sy <= botAnimal.Y + 5; sy++)
                            {
                                var cellToScavenge = nextState.GetCell(sx, sy);
                                if (
                                    cellToScavenge != null
                                    && (
                                        cellToScavenge.Content == CellContent.Pellet
                                        || cellToScavenge.Content == CellContent.PowerPellet
                                    )
                                )
                                {
                                    int scoreFromPellet =
                                        (cellToScavenge.Content == CellContent.PowerPellet)
                                            ? 10
                                            : 1;
                                    if (
                                        botAnimal.ActivePowerUpEffect
                                            == ActivePowerUpType.BigMooseJuice
                                        && botAnimal.PowerUpDurationTicks > 0
                                    )
                                        scoreFromPellet *= 3;
                                    botAnimal.Score += scoreFromPellet;
                                    cellToScavenge.Content = CellContent.Empty;
                                    collectedPelletThisTurn = true; // Scavenging counts as collecting
                                }
                            }
                        }
                        break;
                    case CellContent.BigMooseJuice:
                        botAnimal.ActivePowerUpEffect = ActivePowerUpType.BigMooseJuice;
                        botAnimal.PowerUpDurationTicks = 5;
                        break;
                }
                botAnimal.HeldPowerUp = null; // Consume the power-up
            }
        }
        else // Handle movement
        {
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

            bool isWallCollision = nextState.GetCell(nextX, nextY)?.Content == CellContent.Wall;

            if (!isWallCollision)
            {
                botAnimal.X = nextX;
                botAnimal.Y = nextY;

                var landedCell = nextState.GetCell(nextX, nextY);
                if (landedCell != null)
                {
                    switch (landedCell.Content)
                    {
                        case CellContent.Pellet:
                            int scoreToAdd =
                                (
                                    botAnimal.ActivePowerUpEffect == ActivePowerUpType.BigMooseJuice
                                    && botAnimal.PowerUpDurationTicks > 0
                                )
                                    ? 3
                                    : 1;
                            scoreToAdd *= (1 + Math.Min(botAnimal.ScoreStreak, 3)); // Apply streak multiplier (max x4 for streak 3)
                            botAnimal.Score += scoreToAdd;
                            landedCell.Content = CellContent.Empty;
                            collectedPelletThisTurn = true;
                            GameStateLog.Verbose(
                                "Bot {BotId} collected a pellet at ({X},{Y}). New score: {Score}, Streak: {Streak}",
                                botAnimalId,
                                nextX,
                                nextY,
                                botAnimal.Score,
                                botAnimal.ScoreStreak
                            );
                            break;
                        case CellContent.PowerPellet:
                            int powerPelletScore =
                                (
                                    botAnimal.ActivePowerUpEffect == ActivePowerUpType.BigMooseJuice
                                    && botAnimal.PowerUpDurationTicks > 0
                                )
                                    ? 30
                                    : 10;
                            powerPelletScore *= (1 + Math.Min(botAnimal.ScoreStreak, 3));
                            botAnimal.Score += powerPelletScore;
                            landedCell.Content = CellContent.Empty;
                            collectedPelletThisTurn = true;
                            GameStateLog.Debug(
                                "Bot {BotId} collected a Power Pellet at ({X},{Y}). New score: {Score}, Streak: {Streak}",
                                botAnimalId,
                                nextX,
                                nextY,
                                botAnimal.Score,
                                botAnimal.ScoreStreak
                            );
                            break;
                        case CellContent.ChameleonCloak:
                        case CellContent.Scavenger:
                        case CellContent.BigMooseJuice:
                            if (botAnimal.HeldPowerUp.HasValue) // If holding one, old one is lost
                            {
                                botAnimal.ActivePowerUpEffect = ActivePowerUpType.None;
                                botAnimal.PowerUpDurationTicks = 0;
                            }
                            botAnimal.HeldPowerUp = landedCell.Content;
                            landedCell.Content = CellContent.Empty;
                            GameStateLog.Debug(
                                "Bot {BotId} picked up {PowerUp} at ({X},{Y})",
                                botAnimalId,
                                botAnimal.HeldPowerUp,
                                nextX,
                                nextY
                            );
                            break;
                    }
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
            }
        }

        // Update Score Streak
        if (collectedPelletThisTurn)
        {
            botAnimal.ScoreStreak = Math.Min(botAnimal.ScoreStreak + 1, 3); // Max streak 3 for x4 multiplier
        }
        else
        {
            // GameRules: streak resets if animal does not pick up any pellets for 3 consecutive ticks.
            // This is harder to track perfectly in simple simulation. For now, reset if no pellet this turn.
            // A more accurate simulation would need a 'ticksSinceLastPellet' counter on Animal.
            botAnimal.ScoreStreak = 0;
        }

        // Decrement active power-up duration
        if (botAnimal.PowerUpDurationTicks > 0)
        {
            botAnimal.PowerUpDurationTicks--;
            if (botAnimal.PowerUpDurationTicks == 0)
            {
                GameStateLog.Debug(
                    "Bot {BotId} {PowerUp} effect wore off.",
                    botAnimalId,
                    botAnimal.ActivePowerUpEffect
                );
                botAnimal.ActivePowerUpEffect = ActivePowerUpType.None;
            }
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
                if (
                    !(
                        botAnimal.ActivePowerUpEffect == ActivePowerUpType.ChameleonCloak
                        && botAnimal.PowerUpDurationTicks > 0
                    )
                )
                {
                    evalScore += parameters.Weight_ZkThreat / distToZk;
                }
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
        Cell? closestPellet = null; // Indicate that closestPellet can be null
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

        // Power-up evaluation
        if (
            botAnimal.HeldPowerUp.HasValue
            && botAnimal.HeldPowerUp.Value != CellContent.PowerPellet
        )
        {
            // Simple bonus for holding a usable power-up. Could be more nuanced based on type.
            evalScore += parameters.Weight_PowerUp * 5; // Example: Weight_PowerUp might be 10, so +50.
        }
        if (
            botAnimal.ActivePowerUpEffect != ActivePowerUpType.None
            && botAnimal.PowerUpDurationTicks > 0
        )
        {
            // Bonus for having an active beneficial power-up
            double effectBonus = 0;
            switch (botAnimal.ActivePowerUpEffect)
            {
                case ActivePowerUpType.BigMooseJuice:
                    effectBonus = parameters.Weight_PowerUp * botAnimal.PowerUpDurationTicks * 0.5; // Value based on duration
                    break;
                case ActivePowerUpType.Scavenger:
                    effectBonus = parameters.Weight_PowerUp * botAnimal.PowerUpDurationTicks * 0.7; // Scavenger is generally good
                    break;
                // ChameleonCloak's benefit is handled by reducing ZK threat directly.
            }
            evalScore += effectBonus;
        }

        // Score streak evaluation
        // Add a bonus for maintaining a streak, proportional to its level
        evalScore += botAnimal.ScoreStreak * parameters.Weight_ScoreStreakBonus;

        // TODO: Add evaluation for parameters.Weight_EscapeProgress
        // This would require knowing how these are represented in the game state (e.g., exit points)

        // This log can be very spammy, changing to Verbose.
        // GameStateLog.Verbose(
        //     "Evaluate for Bot {BotId} at ({X},{Y}), Tick {Tick}, SimScore {Score}. Animal Score: {AnimalScore}",
        //     botAnimalId,
        //     botAnimal.X,
        //     botAnimal.Y,
        //     Tick,
        //     evalScore,
        //     botAnimal.Score
        // );
        return evalScore;
    }
}
