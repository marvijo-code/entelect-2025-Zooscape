using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;
using System;
using System.Collections.Generic;

namespace Marvijo.Zooscape.Bots.Common
{
    public class HeuristicContext : IHeuristicContext
    {
        public GameState CurrentGameState { get; }
        public Animal CurrentAnimal { get; }
        public BotAction CurrentMove { get; }
        public ILogger Logger { get; }
        public (int X, int Y) MyNewPosition { get; }
        public BotAction? PreviousAction { get; }
        public Queue<(int X, int Y)> AnimalRecentPositions { get; }
        public BotAction? AnimalLastDirection { get; }
        public HeuristicWeights Weights { get; }
        private readonly IReadOnlyDictionary<(int X, int Y), int> _visitCountsData;
        private readonly ISet<int> _visitedQuadrantsData;

        public HeuristicContext(
            GameState currentGameState,
            Animal currentAnimal,
            BotAction currentMove,
            ILogger logger,
            HeuristicWeights weights,
            BotAction? previousAction = null,
            IReadOnlyDictionary<(int X, int Y), int>? visitCounts = null,
            ISet<int>? visitedQuadrants = null,
            Queue<(int X, int Y)>? animalRecentPositions = null,
            BotAction? animalLastDirection = null
        )
        {
            CurrentGameState = currentGameState;
            CurrentAnimal = currentAnimal;
            CurrentMove = currentMove;
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Weights = weights ?? throw new ArgumentNullException(nameof(weights));
            PreviousAction = previousAction;
            AnimalRecentPositions = animalRecentPositions ?? new Queue<(int X, int Y)>();
            AnimalLastDirection = animalLastDirection;

            MyNewPosition = BotUtils.ApplyMove(CurrentAnimal.X, CurrentAnimal.Y, CurrentMove);
            _visitCountsData = visitCounts ?? new Dictionary<(int X, int Y), int>();
            _visitedQuadrantsData = visitedQuadrants ?? new HashSet<int>();
        }

        public int GetVisitCount((int X, int Y) position)
        {
            return _visitCountsData.TryGetValue(position, out var count) ? count : 0;
        }

        public bool IsQuadrantVisited(int quadrant)
        {
            return _visitedQuadrantsData.Contains(quadrant);
        }
    }
}
