using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClingyHeuroBot2.Heuristics;

file record HeuristicScoreDetail(string Name, decimal Score, decimal AccumulatedScore);

public class ScoreLog
{
    public decimal TotalScore { get; }
    public List<string> DetailedLogLines { get; }
    public BotAction Move { get; }

    public ScoreLog(BotAction move, decimal totalScore, List<string> detailedLogLines)
    {
        Move = move;
        TotalScore = totalScore;
        DetailedLogLines = detailedLogLines ?? [];
    }
}

public class HeuristicsManager
{
    private readonly ILogger _logger;
    private readonly List<IHeuristic> _heuristics;
    private readonly HeuristicWeights _weights;

    public HeuristicsManager(ILogger logger, HeuristicWeights weights)
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
            new PowerUpCollectionHeuristic(),
            new ScoreStreakHeuristic(),
            new UseItemHeuristic(),
        ];

        _logger.Information("Heuristics instance created with {HeuristicCount} heuristics.", _heuristics.Count);
    }

    public ScoreLog ScoreMove(
        GameState state,
        Animal me,
        BotAction move,
        bool logHeuristicScores,
        IReadOnlyDictionary<(int X, int Y), int> visitCounts,
        Queue<(int X, int Y)> animalRecentPositionsQueue,
        BotAction? animalLastDirectionValue
    )
    {
        var heuristicContext = new HeuristicContext(
            state,
            me,
            move,
            _logger,
            _weights,
            null, // previousAction is not available here
            visitCounts,
            animalRecentPositionsQueue,
            animalLastDirectionValue
        );

        decimal totalScore = 0m;
        var detailedScoreEntries = new List<HeuristicScoreDetail>();

        foreach (var heuristic in _heuristics)
        {
            decimal score = heuristic.CalculateScore(heuristicContext);

            if (score == 0m) continue;

            totalScore += score;

            if (logHeuristicScores)
            {
                detailedScoreEntries.Add(new HeuristicScoreDetail(heuristic.Name, score, totalScore));
            }
        }

        var currentMoveLogLines = new List<string>();
        if (logHeuristicScores)
        {
            var logBuilder = new StringBuilder();
            logBuilder.AppendLine($"-------------------- Scoring Move: \"{move}\" for Animal: \"{me.Id}\" at ({me.X},{me.Y}) --------------------");
            logBuilder.AppendLine("| Heuristic Name                       | Score        | Accumulated Score |");
            logBuilder.AppendLine("|--------------------------------------|--------------|-------------------|");

            var sortedEntries = detailedScoreEntries.OrderByDescending(e => Math.Abs(e.Score));

            foreach (var entry in sortedEntries)
            {
                logBuilder.AppendLine($"| {entry.Name,-36} | {entry.Score,12:N2} | {entry.AccumulatedScore,17:N2} |");
            }

            logBuilder.AppendLine($"-------------------- Final Score: {totalScore:N4} --------------------");
            currentMoveLogLines.Add(logBuilder.ToString());
        }

        return new ScoreLog(move, totalScore, currentMoveLogLines);
    }
}
