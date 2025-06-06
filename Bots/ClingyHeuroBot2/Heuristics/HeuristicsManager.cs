#pragma warning disable SKEXP0110
using System.Collections.Concurrent;
using System.Linq; // For OrderByDescending
using System.Text;
using ClingyHeuroBot2.Heuristics; // Added for individual heuristic classes
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace Marvijo.Zooscape.Bots.ClingyHeuroBot2.Heuristics;

// Helper record to store score details for sorting
file record HeuristicScoreDetail(
    string Name,
    decimal RawScore,
    decimal Weight,
    decimal Contribution,
    decimal AccumulatedScore
);

public class ScoreLog
{
    public decimal TotalScore { get; }
    public List<string> DetailedLogLines { get; }
    public BotAction Move { get; }

    public ScoreLog(BotAction move, decimal totalScore, List<string> detailedLogLines)
    {
        Move = move;
        TotalScore = totalScore;
        DetailedLogLines = detailedLogLines ?? new List<string>();
    }
}

public class HeuristicsManager
{
    private readonly ILogger _logger;
    private readonly List<IHeuristic> _heuristics;
    private readonly Dictionary<string, decimal> _weights;

    public HeuristicsManager(ILogger logger, Dictionary<string, decimal> weights)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _weights = weights ?? throw new ArgumentNullException(nameof(weights));

        _heuristics =
        [
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
            new UnexploredBonusHeuristic(),
        ];

        _logger.Information(
            "Heuristics instance created with {HeuristicCount} heuristics and {WeightCount} weights.",
            _heuristics.Count,
            _weights.Count
        );
    }

    public ScoreLog ScoreMove(
        GameState state,
        Animal me,
        BotAction move,
        bool logHeuristicScores,
        IReadOnlyDictionary<(int X, int Y), int> visitCounts,
        System.Collections.Generic.Queue<(int X, int Y)> animalRecentPositionsQueue,
        BotAction? animalLastDirectionValue
    )
    {
        var heuristicContext = new HeuristicContext(
            state,
            me,
            move,
            _logger,
            animalLastDirectionValue,
            visitCounts,
            animalRecentPositionsQueue
        );
        decimal totalScore = 0m;
        var currentMoveLogLines = new List<string>();
        var detailedScoreEntries = new List<HeuristicScoreDetail>(); // To store details for sorting

        if (logHeuristicScores)
        {
            currentMoveLogLines.Add(
                string.Format(
                    "-------------------- Scoring Move: \"{0}\" for Animal: \"{1}\" at ({2},{3}) --------------------",
                    move,
                    me.Id,
                    me.X,
                    me.Y
                )
            );
        }

        foreach (var heuristic in _heuristics)
        {
            if (!_weights.TryGetValue(heuristic.Name, out decimal weight))
            {
                // If weight is not found, default to 0, effectively disabling this heuristic
                weight = 0m;
                if (logHeuristicScores)
                {
                    currentMoveLogLines.Add(
                        string.Format(
                            "    WARN: Weight not found for heuristic '{0}'. Defaulting to 0.",
                            heuristic.Name
                        )
                    );
                }
            }

            // Skip calculation if weight is zero
            if (weight == 0m && logHeuristicScores)
            {
                currentMoveLogLines.Add(
                    string.Format(
                        "    Skipping heuristic '{0}' due to zero weight.",
                        heuristic.Name
                    )
                );
                continue;
            }
            if (weight == 0m)
            {
                continue;
            }

            decimal rawScore = heuristic.CalculateRawScore(heuristicContext);
            decimal componentContribution = rawScore * weight;
            totalScore += componentContribution;

            if (logHeuristicScores && componentContribution != 0m)
            {
                // Store details for later sorting and logging
                detailedScoreEntries.Add(
                    new HeuristicScoreDetail(
                        heuristic.Name,
                        rawScore,
                        weight,
                        componentContribution,
                        totalScore // This is the score *after* this component
                    )
                );
            }
        }

        // After processing all heuristics, sort and add detailed scores to log lines
        if (logHeuristicScores)
        {
            // Sort by contribution, descending. Using a stable sort isn't strictly necessary here
            // as the final order of same-contribution items doesn't have a specified tie-breaker rule yet.
            var sortedDetailedScores = detailedScoreEntries
                .OrderByDescending(s => s.Contribution)
                .ToList();
            foreach (var detail in sortedDetailedScores)
            {
                currentMoveLogLines.Add(
                    string.Format(
                        // Adjusted formatting to align with HeuristicLogHelper (-40 for name, 10 for contribution/newscore)
                        "    {0,-40}: Raw={1,8:F4}, Weight={2,8:F4}, Contribution={3,10:F4}, NewScore={4,10:F4}",
                        detail.Name,
                        detail.RawScore,
                        detail.Weight,
                        detail.Contribution,
                        detail.AccumulatedScore
                    )
                );
            }

            // Add total score and end of move logging
            currentMoveLogLines.Add(
                string.Format(
                    "  >>> Total Score for Move \"{0}\": {1}",
                    move,
                    Math.Round(totalScore, 4)
                )
            );
            currentMoveLogLines.Add(
                string.Format(
                    "-------------------- End Scoring Move: \"{0}\" --------------------\n",
                    move
                )
            );
        }

        return new ScoreLog(move, totalScore, currentMoveLogLines);
    }
}
