using Marvijo.Zooscape.Bots.Common;
using Serilog;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace StaticHeuro.Heuristics;

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
    private readonly List<IHeuristic> _essentialHeuristics;
    private readonly HeuristicWeights _weights;
    private const int EARLY_TICK_THRESHOLD = 5; // Use essential heuristics only for first 5 ticks

    public HeuristicsManager(ILogger logger, HeuristicWeights weights)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        if (weights == null)
        {
            Console.WriteLine("ERROR: HeuristicsManager received null weights! Creating default weights.");
            _weights = new HeuristicWeights();
        }
        else
        {
            _weights = weights;
        }

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
            new ImmediatePelletBonusHeuristic(),
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
            new PelletClusterPlanningHeuristic(),
            new PelletRatioAwarenessHeuristic(),
            new PositionalDominanceHeuristic(),
            new QuadrantAwarenessHeuristic(),
            new RecalcWindowSafetyHeuristic(),
            new ReverseMovePenaltyHeuristic(),
            new OscillationPenaltyHeuristic(),
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
            
            new ZookeeperCooldownHeuristic(),
            new LongTermPelletSeekingHeuristic(),
            new ZookeeperPredictionHeuristic(),
            new PowerUpCollectionHeuristic(),
            new ScoreStreakHeuristic(),
            new UseItemHeuristic(),
        ];

        // Essential heuristics for early tick performance optimization
        _essentialHeuristics =
        [
            new CaptureAvoidanceHeuristic(),
            new ImmediatePelletBonusHeuristic(),
            new PelletEfficiencyHeuristic(),
            new LineOfSightPelletsHeuristic(),
            new PelletClusterPlanningHeuristic(),
            new MoveIfIdleHeuristic(),
            new ReverseMovePenaltyHeuristic(),
            new OscillationPenaltyHeuristic(),
            new EarlyGameZookeeperAvoidanceHeuristic(),
            new EmptyCellAvoidanceHeuristic()
        ];

        _logger.Information("Heuristics instance created with {HeuristicCount} heuristics.", _heuristics.Count);
    }

    public ScoreLog ScoreMove(
        GameState state,
        Animal me,
        BotAction move,
        bool logHeuristicScores,
        IReadOnlyDictionary<(int X, int Y), int> visitCounts,
        Queue<(int X, int Y)> animalRecentPositions,
        BotAction? animalLastDirection,
        System.Diagnostics.Stopwatch? budgetStopwatch = null,
        int? softBudgetMs = null,
        bool forceEssentialOnly = false
    )
    {
        var heuristicContext = new HeuristicContext(
            state,
            me,
            move,
            _logger,
            _weights,
            previousAction: null, // Assuming null or determine if HeuroBotService._previousAction should be passed
            visitCounts: visitCounts,
            visitedQuadrants: null, // Assuming null or determine if this needs to be passed
            animalRecentPositions: animalRecentPositions,
            animalLastDirection: animalLastDirection
        );

        decimal totalScore = 0m;
        var detailedScoreEntries = new List<HeuristicScoreDetail>();

        // Decide if we need to restrict to essential heuristics (early game or forced due to late ticks)
        var heuristicsToUse = (state.Tick <= EARLY_TICK_THRESHOLD || forceEssentialOnly)
            ? _essentialHeuristics
            : _heuristics;
        
        if (logHeuristicScores && state.Tick <= EARLY_TICK_THRESHOLD)
        {
            _logger.Information("Early tick optimization: Using {EssentialCount} essential heuristics instead of {TotalCount} for tick {Tick}", 
                _essentialHeuristics.Count, _heuristics.Count, state.Tick);
        }

        var heuristicTimings = new List<(string Name, long DurationMs)>();
        
        foreach (var heuristic in heuristicsToUse)
        {
            try
            {
                // Budget guard: stop early if we've consumed soft time budget
                if (budgetStopwatch != null && softBudgetMs.HasValue && budgetStopwatch.ElapsedMilliseconds > softBudgetMs.Value)
                {
                    if (logHeuristicScores)
                    {
                        _logger.Debug("[BudgetGuard] Soft budget of {Budget}ms exceeded after {Elapsed}ms – stopping heuristic loop early.", softBudgetMs.Value, budgetStopwatch.ElapsedMilliseconds);
                    }
                    break;
                }

                // Start timing this heuristic
                var heuristicStopwatch = Stopwatch.StartNew();
                decimal score = heuristic.CalculateScore(heuristicContext);
                heuristicStopwatch.Stop();
                
                // Record timing data
                heuristicTimings.Add((heuristic.Name, heuristicStopwatch.ElapsedMilliseconds));

                if (score == 0m) continue;

                totalScore += score;

                if (logHeuristicScores)
                {
                    detailedScoreEntries.Add(new HeuristicScoreDetail(heuristic.Name, score, totalScore));
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Heuristic '{HeuristicName}' threw an exception during calculation", heuristic.Name);
                Log.CloseAndFlush(); // Force logs to be written before the process terminates
                throw; // Re-throw the exception to maintain original behavior
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
        
        // Log slow heuristics above threshold for performance analysis
        if (heuristicTimings.Count > 0)
        {
            const long SLOW_HEURISTIC_THRESHOLD_MS = 5; // Log heuristics taking 5ms or more
            var slowHeuristics = heuristicTimings.Where(h => h.DurationMs >= SLOW_HEURISTIC_THRESHOLD_MS)
                                                .OrderByDescending(h => h.DurationMs)
                                                .ToList();
            
            if (slowHeuristics.Count > 0)
            {
                var totalHeuristicTime = heuristicTimings.Sum(h => h.DurationMs);
                var slowHeuristicsList = string.Join(", ", slowHeuristics.Select(h => $"{h.Name}:{h.DurationMs}ms"));
                
                _logger.Warning("[HeuristicPerf] T{Tick} {Move} Total:{TotalMs}ms - Slow: {SlowHeuristics}", 
                    state.Tick, move, totalHeuristicTime, slowHeuristicsList);
            }
        }

        return new ScoreLog(move, totalScore, currentMoveLogLines);
    }
}
