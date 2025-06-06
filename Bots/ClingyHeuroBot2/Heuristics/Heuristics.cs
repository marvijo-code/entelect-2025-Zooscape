#pragma warning disable SKEXP0110
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ClingyHeuroBot2.Heuristics;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics
{
    public static class Heuristics
    {
        public static (int x, int y) ApplyMove(int x, int y, BotAction m) =>
            m switch
            {
                BotAction.Up => (x, y - 1),
                BotAction.Down => (x, y + 1),
                BotAction.Left => (x - 1, y),
                BotAction.Right => (x + 1, y),
                _ => (x, y),
            };

        public static int ManhattanDistance(int x1, int y1, int x2, int y2) =>
            Math.Abs(x1 - x2) + Math.Abs(y1 - y2);

        public static bool IsTraversable(GameState state, int x, int y)
        {
            // Check if the cell exists and is not a wall
            var cell = state.Cells.FirstOrDefault(c => c.X == x && c.Y == y);
            return cell != null && cell.Content != CellContent.Wall;
        }
    }

    public class HeuristicsManager
    {
        // Keep track of recently visited positions to avoid cycles
        public static readonly ConcurrentDictionary<string, Queue<(int, int)>> _recentPositions =
            new ConcurrentDictionary<string, Queue<(int, int)>>();

        // Track the last direction for each animal to detect reversals and turns
        private static readonly ConcurrentDictionary<string, BotAction> _lastDirections =
            new ConcurrentDictionary<string, BotAction>();

        private readonly ILogger _logger;
        private readonly HeuristicLogHelper _logHelper; // Assuming HeuristicLogHelper will be defined or provided
        private readonly List<IHeuristic> _heuristics;
        private readonly Dictionary<string, decimal> _weights;

        public HeuristicsManager(
            ILogger logger,
            HeuristicLogHelper logHelper,
            Dictionary<string, decimal> weights
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logHelper = logHelper ?? throw new ArgumentNullException(nameof(logHelper)); // Assuming HeuristicLogHelper is a class/interface
            _weights = weights ?? throw new ArgumentNullException(nameof(weights));

            _heuristics = new List<IHeuristic>
            {
                new AdaptivePathfindingHeuristic(),
                new AnimalCongestionHeuristic(),
                new AnticipateCompetitionHeuristic(),
                new AreaControlHeuristic(),
                new CaptureAvoidanceHeuristic(),
                new CaptureRecoveryStrategyHeuristic(),
                new CenterControlHeuristic(),
                new CenterDistanceBonusHeuristic(),
                new ChangeDirectionWhenStuckHeuristic(),
                new CommandQueueOptimizationHeuristic(),
                new CornerControlHeuristic(),
                new CycleDetectionHeuristic(),
                new DensityMappingHeuristic(),
                new DirectionalVarietyHeuristic(),
                new DistanceToGoalHeuristic(),
                new EarlyGameZookeeperAvoidanceHeuristic(),
                new EdgeAwarenessHeuristic(),
                new EdgeSafetyHeuristic(),
                new EmptyCellAvoidanceHeuristic(),
                new EndgameStrategyHeuristic(),
                new EscapeRoutesHeuristic(),
                new FirstCommandAdvantageHeuristic(),
                new LineOfSightPelletsHeuristic(),
                new MobilityHeuristic(),
                new MoveIfIdleHeuristic(),
                new MovementConsistencyHeuristic(),
                new OpponentProximityHeuristic(),
                new OpponentTrailChasingHeuristic(),
                new PathSafetyHeuristic(),
                new PelletAreaControlHeuristic(),
                new PelletEfficiencyHeuristic(),
                new PelletRaceHeuristic(),
                new PelletRatioAwarenessHeuristic(),
                new PositionalDominanceHeuristic(),
                new QuadrantAwarenessHeuristic(),
                new RecalcWindowSafetyHeuristic(),
                new ResourceClusteringHeuristic(),
                new ScoreLossMinimizerHeuristic(),
                new ShortestPathToGoalHeuristic(),
                new SpawnProximityHeuristic(),
                new SpawnTimeMinimizerHeuristic(),
                new TargetEvaluationHeuristic(),
                new TiebreakersAwarenessHeuristic(),
                new TimeToCaptureHeuristic(),
                new TimerAwarenessHeuristic(),
                new TravelDistanceMaximizerHeuristic(),
                new TunnelNavigationHeuristic(),
                new UnoccupiedCellBonusHeuristic(),
                new WallCollisionRiskHeuristic(),
                new ZookeeperCooldownHeuristic(),
                new ZookeeperPredictionHeuristic(),
            };

            _logger.Information(
                "Heuristics instance created with {HeuristicCount} heuristics and {WeightCount} weights.",
                _heuristics.Count,
                _weights.Count
            );
        }

        public decimal ScoreMove(
            GameState state,
            Animal me,
            BotAction move,
            bool logHeuristicScores
        )
        {
            decimal totalScore = 0m;

            if (logHeuristicScores)
            {
                _logger.Information(
                    "Scoring move {MoveDirection} for animal {AnimalId} at ({X},{Y}):",
                    move,
                    me.Id,
                    me.X,
                    me.Y
                );
            }

            foreach (var heuristic in _heuristics)
            {
                if (!_weights.TryGetValue(heuristic.Name, out decimal weight))
                {
                    // If weight is not found, default to 0, effectively disabling this heuristic
                    weight = 0m;
                    if (logHeuristicScores) // Log only if detailed logging is enabled
                    {
                        _logger.Warning(
                            "    Weight not found for heuristic '{HeuristicName}'. Defaulting to 0.",
                            heuristic.Name
                        );
                    }
                }

                // Skip calculation if weight is zero
                if (weight == 0m && logHeuristicScores)
                {
                    _logger.Information(
                        "    Skipping heuristic '{HeuristicName}' due to zero weight.",
                        heuristic.Name
                    );
                    continue;
                }
                if (weight == 0m)
                {
                    continue;
                }

                decimal rawScore = heuristic.CalculateRawScore(state, me, move, _logger);
                decimal componentContribution = rawScore * weight;
                totalScore += componentContribution;

                _logHelper.LogScoreComponent(
                    _logger,
                    logHeuristicScores,
                    heuristic.Name,
                    rawScore,
                    weight,
                    componentContribution,
                    totalScore
                );
            }

            if (logHeuristicScores)
            {
                _logger.Information(
                    "  Final score for move {MoveDirection}: {FinalScore}",
                    move,
                    Math.Round(totalScore, 4)
                );
            }

            return totalScore;
        }

        // Reset counters if needed based on game state
        private static void UpdatePathMemory(GameState state, Animal me)
        {
            string animalKey = me.Id.ToString();

            // Initialize or get position history for this animal
            if (!_recentPositions.ContainsKey(animalKey))
            {
                _recentPositions[animalKey] = new Queue<(int, int)>();
            }

            // Add current position to history
            var positions = _recentPositions[animalKey];
            positions.Enqueue((me.X, me.Y));

            // Keep only the most recent 10 positions to detect cycles
            while (positions.Count > 10)
            {
                positions.Dequeue();
            }

            // Clear history if animal was just captured (at spawn point)
            if (me.X == me.SpawnX && me.Y == me.SpawnY)
            {
                positions.Clear();
                _lastDirections.TryRemove(animalKey, out _);
            }
        }
    }
}
