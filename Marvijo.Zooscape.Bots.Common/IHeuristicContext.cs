#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Collections.Generic;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models; // Added for GameState and Animal
using Serilog; // Added for ILogger

namespace Marvijo.Zooscape.Bots.Common
{
    /// <summary>
    /// Provides bot-specific context to heuristics during score calculation.
    /// This context includes information about the bot's history and state
    /// that is not part of the general GameState.
    /// </summary>
    public interface IHeuristicContext
    {
        /// <summary>
        /// Gets the previous action taken by the bot.
        /// Null if this is the first move or previous action is not tracked.
        /// </summary>
        BotAction? PreviousAction { get; }

        /// <summary>
        /// Gets the number of times the bot has visited the specified position.
        /// </summary>
        /// <param name="position">The (X, Y) coordinates of the position.</param>
        /// <returns>The visit count for the position; 0 if never visited.</returns>
        int GetVisitCount((int X, int Y) position);

        /// <summary>
        /// Checks if the specified game quadrant has been visited by the bot.
        /// Quadrants are typically numbered (e.g., 0-3 or 1-4).
        /// </summary>
        /// <param name="quadrant">The identifier of the quadrant.</param>
        /// <returns>True if the quadrant has been visited, false otherwise.</returns>
        bool IsQuadrantVisited(int quadrant);

        /// <summary>
        /// Gets the current game state.
        /// </summary>
        GameState CurrentGameState { get; }

        /// <summary>
        /// Gets the current animal (the bot itself).
        /// </summary>
        Animal CurrentAnimal { get; }

        /// <summary>
        /// Gets the move being evaluated.
        /// </summary>
        BotAction CurrentMove { get; }

        /// <summary>
        /// Gets the logger instance for heuristic logging.
        /// </summary>
        ILogger Logger { get; }

        /// <summary>
        /// Gets the queue of recently visited positions by the current animal.
        /// </summary>
        System.Collections.Generic.Queue<(int X, int Y)> AnimalRecentPositions { get; }

        /// <summary>
        /// Gets the last committed direction of the current animal.
        /// Null if no direction has been committed yet or not tracked.
        /// </summary>
        BotAction? AnimalLastDirection { get; }

        /// <summary>
        /// Gets the calculated new position (X, Y) after applying CurrentMove to CurrentAnimal.
        /// </summary>
        (int X, int Y) MyNewPosition { get; }
    }
}
