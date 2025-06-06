using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;

namespace Marvijo.Zooscape.Bots.Common
{
    public class HeuristicContext : IHeuristicContext
    {
        public GameState CurrentGameState { get; }
        public Animal CurrentAnimal { get; }
        public BotAction CurrentMove { get; }
        public ILogger Logger { get; } // Made non-nullable
        public (int X, int Y) MyNewPosition { get; }
        public BotAction? PreviousAction { get; } // Can be expanded later
        public System.Collections.Generic.Queue<(int X, int Y)> AnimalRecentPositions { get; } // Added
        public BotAction? AnimalLastDirection { get; } // Added
        private readonly IReadOnlyDictionary<(int X, int Y), int> _visitCountsData;

        public HeuristicContext(
            GameState currentGameState,
            Animal currentAnimal,
            BotAction currentMove,
            ILogger logger, // Made non-nullable
            BotAction? previousAction = null,
            IReadOnlyDictionary<(int X, int Y), int>? visitCounts = null,
            System.Collections.Generic.Queue<(int X, int Y)>? animalRecentPositions = null, // Added
            BotAction? animalLastDirection = null // Added
        )
        {
            CurrentGameState = currentGameState;
            CurrentAnimal = currentAnimal;
            CurrentMove = currentMove;
            Logger = logger ?? throw new System.ArgumentNullException(nameof(logger)); // Added null check for non-nullable
            PreviousAction = previousAction;
            AnimalRecentPositions =
                animalRecentPositions ?? new System.Collections.Generic.Queue<(int X, int Y)>(); // Added
            AnimalLastDirection = animalLastDirection; // Added

            MyNewPosition = BotUtils.ApplyMove(CurrentAnimal.X, CurrentAnimal.Y, CurrentMove);
            _visitCountsData = visitCounts ?? new Dictionary<(int X, int Y), int>();
        }

        // Stubbed implementations for other interface members
        public int GetVisitCount((int X, int Y) position)
        {
            return _visitCountsData.TryGetValue(position, out var count) ? count : 0;
        }

        public bool IsQuadrantVisited(int quadrant)
        {
            // TODO: Implement actual quadrant visit tracking if needed
            return false;
        }
    }
}
