using System.Diagnostics;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;
using Serilog.Core;
using ClingyHeuroBot2Service = ClingyHeuroBot2.Services.HeuroBotService;

namespace MonteCarloBot.Services;

public sealed class MonteCarloBotService : IBot<MonteCarloBotService>
{
    private const int PointsPerPellet = 64;
    private const int PowerPelletMultiplier = 10;
    private const int BigMooseMultiplier = 3;
    private const int CaptureLossPercentage = 10;
    private const int ScoreStreakResetGrace = 3;
    private const int ScavengerRadius = 5;
    private const int ScavengerDuration = 5;
    private const int BigMooseDuration = 5;
    private const int CloakDuration = 20;
    private const int ZookeeperRetargetInterval = 20;
    private const int SafetyModeTick = 1400;
    private const int SafetyModeScore = 35000;
    private const int SafetyModeLead = 12000;
    private const int SafetyModeZookeeperCount = 3;
    private const int DefaultTimeBudgetMs = 4;
    private const int EarlyTimeBudgetBonusMs = 8;
    private const int LateTimeBudgetMs = 2;
    private const int SimulationHorizon = 34;
    private const double RootExploration = 1.15;
    private const double HybridOverrideMargin = 180.0;
    private const int LocalPotentialRadius = 4;
    private const int MonteCarloThreatDistance = 2;
    private const int EmergencySafetyDistance = 3;
    private const int MonteCarloCollectibleThreshold = 7;
    private const int PostCaptureEscapeTicks = 10;
    private const int PostCaptureSpawnRadius = 4;
    private const int PostCaptureDangerDistance = 4;
    private static readonly BotAction[] MovementActions =
    [
        BotAction.Up,
        BotAction.Down,
        BotAction.Left,
        BotAction.Right,
    ];

    private readonly ILogger _logger;
    private ClingyHeuroBot2Service _fallbackBot;
    private LayoutCache? _layout;
    private int _lastCapturedCounter = -1;
    private int _postCaptureEscapeTicksRemaining;

    public MonteCarloBotService(ILogger? logger = null)
    {
        _logger = logger ?? Logger.None;
        _fallbackBot = new ClingyHeuroBot2Service(_logger);
    }

    public Guid BotId { get; set; }

    public void SetBotId(Guid botId)
    {
        BotId = botId;
        _fallbackBot.BotId = botId;
    }

    public BotCommand ProcessState(GameState state)
    {
        return new BotCommand { Action = GetAction(state) };
    }

    public BotAction GetAction(GameState gameState)
    {
        if (gameState?.Animals == null || gameState.Cells == null || gameState.Cells.Count == 0)
        {
            return BotAction.Up;
        }

        var me = gameState.Animals.FirstOrDefault(a => a.Id == BotId)
            ?? gameState.MyAnimal
            ?? gameState.Animals.FirstOrDefault();

        if (me is null)
        {
            return BotAction.Up;
        }

        UpdateCaptureState(me);
        var layout = EnsureLayout(gameState);
        var root = BuildRootState(gameState, me, layout);
        if (!ShouldConsiderMonteCarlo(root))
        {
            var baselineAction = GetFallbackAction(gameState, root);
            return GetSafetyOverride(gameState, me, root, baselineAction);
        }

        var fallbackAction = GetFallbackAction(gameState, root);
        var primaryAction = GetPrimaryAction(gameState, root, fallbackAction);
        return GetSafetyOverride(gameState, me, root, primaryAction);
    }

    private static bool ShouldConsiderMonteCarlo(RootState root)
    {
        if (root.CandidateCount <= 1)
        {
            return false;
        }

        if (root.HeldPower != SimPowerType.None || (root.ActivePower != SimPowerType.None && root.ActiveTicks > 0))
        {
            return true;
        }

        if (GetNearestRootDangerDistance(root, root.MyIndex) <= MonteCarloThreatDistance)
        {
            return true;
        }

        if (CountNearbyCollectibles(root.Layout, root.Walls, root.Collectibles, root.MyIndex) >= MonteCarloCollectibleThreshold)
        {
            return true;
        }

        foreach (var candidate in root.Candidates)
        {
            if (candidate == BotAction.UseItem)
            {
                return true;
            }

            var next = GetNextIndex(root.Layout, root.MyIndex, candidate);
            if (next >= 0 && !root.Walls[next] && root.Collectibles[next] >= CollectibleKind.Cloak)
            {
                return true;
            }
        }

        return false;
    }

    private BotAction GetPrimaryAction(GameState gameState, RootState root, BotAction fallbackAction)
    {
        if (!ShouldUseMonteCarloOverride(root, fallbackAction))
        {
            return fallbackAction;
        }

        if (root.CandidateCount <= 0)
        {
            return fallbackAction;
        }

        if (root.CandidateCount == 1)
        {
            return root.Candidates[0];
        }

        Span<double> sums = stackalloc double[5];
        Span<int> visits = stackalloc int[5];
        var rng = new FastRandom(
            HashSeed(
                gameState.Tick,
                root.Layout.XByIndex[root.MyIndex],
                root.Layout.YByIndex[root.MyIndex],
                root.Score));
        var totalVisits = 0;
        var budget = GetTimeBudgetMs(gameState.Tick);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < budget)
        {
            var candidateIndex = SelectRootCandidate(root.CandidateCount, sums, visits, totalVisits);
            var score = Simulate(root, root.Candidates[candidateIndex], ref rng);
            sums[candidateIndex] += score;
            visits[candidateIndex]++;
            totalVisits++;
        }

        if (totalVisits <= 0)
        {
            return fallbackAction;
        }

        var bestIndex = FindCandidateIndex(root.Candidates, fallbackAction);
        if (bestIndex < 0)
        {
            bestIndex = 0;
        }

        var bestAverage = double.NegativeInfinity;
        for (var i = 0; i < root.CandidateCount; i++)
        {
            if (visits[i] <= 0)
            {
                continue;
            }

            var average = sums[i] / visits[i];
            if (average > bestAverage)
            {
                bestAverage = average;
                bestIndex = i;
            }
        }

        var fallbackIndex = FindCandidateIndex(root.Candidates, fallbackAction);
        if (fallbackIndex >= 0 && visits[fallbackIndex] > 0)
        {
            var fallbackAverage = sums[fallbackIndex] / visits[fallbackIndex];
            if (bestIndex == fallbackIndex || bestAverage < fallbackAverage + HybridOverrideMargin)
            {
                return fallbackAction;
            }
        }

        return root.Candidates[bestIndex];
    }

    private static bool ShouldUseMonteCarloOverride(RootState root, BotAction fallbackAction)
    {
        if (fallbackAction == BotAction.UseItem)
        {
            return true;
        }

        if (!IsLegalAction(root, fallbackAction))
        {
            return true;
        }

        var fallbackIndex = GetNextIndex(root.Layout, root.MyIndex, fallbackAction);
        if (fallbackIndex >= 0 && !root.Walls[fallbackIndex])
        {
            if (root.Collectibles[fallbackIndex] is CollectibleKind.Pellet or CollectibleKind.PowerPellet)
            {
                return false;
            }

            if (root.Collectibles[fallbackIndex] >= CollectibleKind.Cloak)
            {
                return true;
            }
        }

        if (root.HeldPower != SimPowerType.None || (root.ActivePower != SimPowerType.None && root.ActiveTicks > 0))
        {
            return true;
        }

        var nearbyCollectibles = CountNearbyCollectibles(root.Layout, root.Walls, root.Collectibles, root.MyIndex);
        if (nearbyCollectibles >= MonteCarloCollectibleThreshold && GetNearestRootDangerDistance(root, root.MyIndex) <= MonteCarloThreatDistance)
        {
            return true;
        }

        return GetNearestRootDangerDistance(root, root.MyIndex) <= MonteCarloThreatDistance;
    }

    private static int GetTimeBudgetMs(int tick)
    {
        if (tick <= 80)
        {
            return DefaultTimeBudgetMs + EarlyTimeBudgetBonusMs;
        }

        if (tick >= 800)
        {
            return LateTimeBudgetMs;
        }

        return DefaultTimeBudgetMs;
    }

    private BotAction GetSafetyOverride(GameState gameState, Animal me, RootState root, BotAction fallbackAction)
    {
        var usePostCaptureEscape = ShouldUsePostCaptureEscape(root);
        if (!ShouldUseSafetyMode(gameState, me) && !ShouldUseEmergencySafety(root, fallbackAction) && !usePostCaptureEscape)
        {
            return fallbackAction;
        }

        var evaluators = new Dictionary<int, int[]>();
        var fallbackEvaluation = EvaluateSafetyAction(root, me, fallbackAction, fallbackAction, evaluators);
        if (!usePostCaptureEscape
            && fallbackEvaluation.IsValid
            && !fallbackEvaluation.FatalNow
            && !fallbackEvaluation.FatalAfterMove)
        {
            return fallbackAction;
        }

        if (me.HeldPowerUp == PowerUpType.ChameleonCloak)
        {
            var cloakEvaluation = EvaluateSafetyAction(root, me, BotAction.UseItem, fallbackAction, evaluators);
            if (cloakEvaluation.IsValid && !cloakEvaluation.FatalNow && !cloakEvaluation.FatalAfterMove)
            {
                return BotAction.UseItem;
            }
        }

        SafetyEvaluation? best = null;
        foreach (var candidate in root.Candidates)
        {
            var evaluation = EvaluateSafetyAction(root, me, candidate, fallbackAction, evaluators);
            if (!evaluation.IsValid)
            {
                continue;
            }

            if (best is null
                || (usePostCaptureEscape
                    ? IsBetterPostCaptureEscapeEvaluation(evaluation, best.Value)
                    : IsBetterSafetyEvaluation(evaluation, best.Value)))
            {
                best = evaluation;
            }
        }

        return best?.Action ?? fallbackAction;
    }

    private static bool ShouldUseSafetyMode(GameState gameState, Animal me)
    {
        var bestOpponentScore = gameState.Animals
            .Where(a => a.Id != me.Id)
            .Select(a => a.Score)
            .DefaultIfEmpty(0)
            .Max();

        return gameState.Tick >= SafetyModeTick
            || me.Score >= SafetyModeScore
            || me.Score >= bestOpponentScore + SafetyModeLead
            || gameState.Zookeepers.Count >= SafetyModeZookeeperCount;
    }

    private static bool ShouldUseEmergencySafety(RootState root, BotAction fallbackAction)
    {
        if (GetNearestRootDangerDistance(root, root.MyIndex) <= EmergencySafetyDistance)
        {
            return true;
        }

        if (!IsLegalAction(root, fallbackAction))
        {
            return true;
        }

        var fallbackIndex = fallbackAction == BotAction.UseItem
            ? root.MyIndex
            : GetNextIndex(root.Layout, root.MyIndex, fallbackAction);

        if (fallbackIndex < 0 || root.Walls[fallbackIndex])
        {
            return true;
        }

        return GetNearestRootDangerDistance(root, fallbackIndex) <= EmergencySafetyDistance;
    }

    private void UpdateCaptureState(Animal me)
    {
        if (_lastCapturedCounter != me.CapturedCounter)
        {
            _lastCapturedCounter = me.CapturedCounter;
            _postCaptureEscapeTicksRemaining = me.CapturedCounter > 0 ? PostCaptureEscapeTicks : 0;
            return;
        }

        if (_postCaptureEscapeTicksRemaining > 0)
        {
            _postCaptureEscapeTicksRemaining--;
        }

    }

    private bool ShouldUsePostCaptureEscape(RootState root)
    {
        if (_postCaptureEscapeTicksRemaining <= 0)
        {
            return false;
        }

        return Manhattan(root.Layout, root.MyIndex, root.SpawnIndex) <= PostCaptureSpawnRadius
            || GetNearestRootDangerDistance(root, root.MyIndex) <= PostCaptureDangerDistance;
    }

    private BotAction GetFallbackAction(GameState gameState, RootState root)
    {
        try
        {
            _fallbackBot.BotId = BotId;
            var fallbackAction = _fallbackBot.GetAction(gameState);
            return IsLegalAction(root, fallbackAction) ? fallbackAction : root.Candidates[0];
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Fallback StaticHeuro action failed for tick {Tick}", gameState.Tick);
            return root.Candidates.Length > 0 ? root.Candidates[0] : BotAction.Up;
        }
    }

    private LayoutCache EnsureLayout(GameState state)
    {
        var width = state.Cells.Max(c => c.X) + 1;
        var height = state.Cells.Max(c => c.Y) + 1;

        if (_layout is not null && _layout.Width == width && _layout.Height == height)
        {
            return _layout;
        }

        var cellCount = width * height;
        var xByIndex = new int[cellCount];
        var yByIndex = new int[cellCount];
        var up = new int[cellCount];
        var down = new int[cellCount];
        var left = new int[cellCount];
        var right = new int[cellCount];

        for (var index = 0; index < cellCount; index++)
        {
            var x = index % width;
            var y = index / width;
            xByIndex[index] = x;
            yByIndex[index] = y;
            up[index] = ((y - 1 + height) % height) * width + x;
            down[index] = ((y + 1) % height) * width + x;
            left[index] = y * width + ((x - 1 + width) % width);
            right[index] = y * width + ((x + 1) % width);
        }

        var offsets = new int[(ScavengerRadius * 2 + 1) * (ScavengerRadius * 2 + 1) * 2];
        var offsetCount = 0;
        for (var dy = -ScavengerRadius; dy <= ScavengerRadius; dy++)
        {
            for (var dx = -ScavengerRadius; dx <= ScavengerRadius; dx++)
            {
                offsets[offsetCount++] = dx;
                offsets[offsetCount++] = dy;
            }
        }

        _layout = new LayoutCache(width, height, xByIndex, yByIndex, up, down, left, right, offsets);
        return _layout;
    }

    private RootState BuildRootState(GameState state, Animal me, LayoutCache layout)
    {
        var walls = new bool[layout.CellCount];
        var collectibles = new byte[layout.CellCount];
        foreach (var cell in state.Cells)
        {
            var index = ToIndex(layout, cell.X, cell.Y);
            switch (cell.Content)
            {
                case CellContent.Wall:
                    walls[index] = true;
                    break;
                case CellContent.Pellet:
                    collectibles[index] = CollectibleKind.Pellet;
                    break;
                case CellContent.PowerPellet:
                    collectibles[index] = CollectibleKind.PowerPellet;
                    break;
                case CellContent.ChameleonCloak:
                    collectibles[index] = CollectibleKind.Cloak;
                    break;
                case CellContent.Scavenger:
                    collectibles[index] = CollectibleKind.Scavenger;
                    break;
                case CellContent.BigMooseJuice:
                    collectibles[index] = CollectibleKind.BigMoose;
                    break;
            }
        }

        var myIndex = ToIndex(layout, me.X, me.Y);
        var spawnIndex = ToIndex(layout, me.SpawnX, me.SpawnY);

        var opponents = state.Animals
            .Where(a => a.Id != me.Id)
            .Select(a => new OpponentState(
                ToIndex(layout, a.X, a.Y),
                ToIndex(layout, a.SpawnX, a.SpawnY),
                a.IsViable,
                a.ActivePowerUp?.Type == PowerUpType.ChameleonCloak ? a.ActivePowerUp.TicksRemaining : 0))
            .ToArray();

        var zookeepers = state.Zookeepers
            .Select(z => new ZookeeperState(ToIndex(layout, z.X, z.Y), 0, -1))
            .ToArray();

        var candidates = GetCandidateActions(layout, walls, collectibles, me, myIndex);

        return new RootState(
            layout,
            walls,
            collectibles,
            candidates,
            candidates.Length,
            state.Tick,
            myIndex,
            spawnIndex,
            me.IsViable,
            me.Score,
            me.ScoreStreak,
            ToPowerState(me.ActivePowerUp?.Type),
            me.ActivePowerUp?.TicksRemaining ?? 0,
            ToPowerState(me.HeldPowerUp),
            zookeepers,
            opponents);
    }

    private static BotAction[] GetCandidateActions(LayoutCache layout, bool[] walls, byte[] collectibles, Animal me, int myIndex)
    {
        Span<BotAction> buffer = stackalloc BotAction[5];
        var count = 0;

        foreach (var action in MovementActions)
        {
            var next = GetNextIndex(layout, myIndex, action);
            if (next >= 0 && !walls[next])
            {
                buffer[count++] = action;
            }
        }

        if (ShouldUseItemImmediately(layout, walls, collectibles, me, myIndex))
        {
            buffer[count++] = BotAction.UseItem;
        }

        if (count == 0)
        {
            buffer[count++] = BotAction.Up;
        }

        return buffer[..count].ToArray();
    }

    private static bool ShouldUseItemImmediately(LayoutCache layout, bool[] walls, byte[] collectibles, Animal me, int myIndex)
    {
        if (me.HeldPowerUp is null or PowerUpType.PowerPellet)
        {
            return false;
        }

        return me.HeldPowerUp switch
        {
            PowerUpType.ChameleonCloak => true,
            PowerUpType.Scavenger => CountNearbyCollectibles(layout, walls, collectibles, myIndex) >= 6,
            PowerUpType.BigMooseJuice => CountNearbyCollectibles(layout, walls, collectibles, myIndex) >= 3,
            _ => false,
        };
    }

    private static int CountNearbyCollectibles(LayoutCache layout, bool[] walls, byte[] collectibles, int centerIndex)
    {
        var centerX = layout.XByIndex[centerIndex];
        var centerY = layout.YByIndex[centerIndex];
        var count = 0;

        for (var dy = -2; dy <= 2; dy++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                var x = centerX + dx;
                var y = centerY + dy;
                x = WrapCoord(x, layout.Width);
                y = WrapCoord(y, layout.Height);
                var index = ToIndex(layout, x, y);
                if (!walls[index] && collectibles[index] is CollectibleKind.Pellet or CollectibleKind.PowerPellet)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int SelectRootCandidate(int candidateCount, Span<double> sums, Span<int> visits, int totalVisits)
    {
        for (var i = 0; i < candidateCount; i++)
        {
            if (visits[i] == 0)
            {
                return i;
            }
        }

        var bestIndex = 0;
        var bestScore = double.NegativeInfinity;
        var logTerm = Math.Log(totalVisits + 1.0);

        for (var i = 0; i < candidateCount; i++)
        {
            var score = sums[i] / visits[i] + RootExploration * Math.Sqrt(logTerm / visits[i]);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static int FindCandidateIndex(BotAction[] candidates, BotAction action)
    {
        for (var i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] == action)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsLegalAction(RootState root, BotAction action)
    {
        if (action == BotAction.UseItem)
        {
            return root.HeldPower != SimPowerType.None;
        }

        var next = GetNextIndex(root.Layout, root.MyIndex, action);
        return next >= 0 && !root.Walls[next];
    }

    private static SafetyEvaluation EvaluateSafetyAction(
        RootState root,
        Animal me,
        BotAction action,
        BotAction fallbackAction,
        Dictionary<int, int[]> distancesCache)
    {
        if (!IsLegalAction(root, action))
        {
            return new SafetyEvaluation(action, false, true, true, int.MinValue, 0, 0, false, 0);
        }

        var resultIndex = action == BotAction.UseItem ? root.MyIndex : GetNextIndex(root.Layout, root.MyIndex, action);
        var fatalNow = root.Zookeepers.Any(z => z.Index == resultIndex);
        var cloakAfterAction =
            root.ActivePower == SimPowerType.Cloak && root.ActiveTicks > 0
            || (action == BotAction.UseItem && root.HeldPower == SimPowerType.Cloak);
        var myViableAfterAction = root.MyViable;
        var predictedZookeepers = PredictZookeeperPositions(root, resultIndex, myViableAfterAction, cloakAfterAction, distancesCache);
        var fatalAfterMove = predictedZookeepers.Any(index => index == resultIndex);
        var exitCount = CountExits(root, resultIndex);
        var pathDistances = GetDistances(root, resultIndex, distancesCache);
        var nearestAfterMove = predictedZookeepers
            .Select(index => pathDistances[index])
            .Where(distance => distance >= 0)
            .DefaultIfEmpty(int.MaxValue)
            .Min();
        var trapPenalty = exitCount <= 2 && nearestAfterMove <= 2 ? 100 : 0;
        var spawnDistances = GetDistances(root, root.SpawnIndex, distancesCache);
        var spawnDistance = spawnDistances[resultIndex];
        var collectibleScore = root.Collectibles[resultIndex] switch
        {
            CollectibleKind.PowerPellet => 3,
            CollectibleKind.Pellet => 2,
            CollectibleKind.Cloak => 2,
            CollectibleKind.Scavenger => 1,
            CollectibleKind.BigMoose => 1,
            _ => 0,
        };

        return new SafetyEvaluation(
            action,
            true,
            fatalNow,
            fatalAfterMove,
            nearestAfterMove == int.MaxValue ? 999 : nearestAfterMove,
            exitCount,
            collectibleScore - trapPenalty,
            action == fallbackAction,
            spawnDistance < 0 ? 999 : spawnDistance);
    }

    private static bool IsBetterSafetyEvaluation(SafetyEvaluation candidate, SafetyEvaluation incumbent)
    {
        if (candidate.FatalNow != incumbent.FatalNow)
        {
            return !candidate.FatalNow;
        }

        if (candidate.FatalAfterMove != incumbent.FatalAfterMove)
        {
            return !candidate.FatalAfterMove;
        }

        if (candidate.NearestAfterMove != incumbent.NearestAfterMove)
        {
            return candidate.NearestAfterMove > incumbent.NearestAfterMove;
        }

        if (candidate.ExitCount != incumbent.ExitCount)
        {
            return candidate.ExitCount > incumbent.ExitCount;
        }

        if (candidate.CollectibleScore != incumbent.CollectibleScore)
        {
            return candidate.CollectibleScore > incumbent.CollectibleScore;
        }

        if (candidate.IsFallback != incumbent.IsFallback)
        {
            return !candidate.IsFallback;
        }

        return false;
    }

    private static bool IsBetterPostCaptureEscapeEvaluation(SafetyEvaluation candidate, SafetyEvaluation incumbent)
    {
        if (candidate.FatalNow != incumbent.FatalNow)
        {
            return !candidate.FatalNow;
        }

        if (candidate.FatalAfterMove != incumbent.FatalAfterMove)
        {
            return !candidate.FatalAfterMove;
        }

        if (candidate.NearestAfterMove != incumbent.NearestAfterMove)
        {
            return candidate.NearestAfterMove > incumbent.NearestAfterMove;
        }

        if (candidate.SpawnDistance != incumbent.SpawnDistance)
        {
            return candidate.SpawnDistance > incumbent.SpawnDistance;
        }

        if (candidate.ExitCount != incumbent.ExitCount)
        {
            return candidate.ExitCount > incumbent.ExitCount;
        }

        if (candidate.CollectibleScore != incumbent.CollectibleScore)
        {
            return candidate.CollectibleScore > incumbent.CollectibleScore;
        }

        if (candidate.IsFallback != incumbent.IsFallback)
        {
            return candidate.IsFallback;
        }

        return false;
    }

    private static int[] PredictZookeeperPositions(
        RootState root,
        int myIndex,
        bool myViable,
        bool myCloaked,
        Dictionary<int, int[]> distancesCache)
    {
        var predicted = new int[root.Zookeepers.Length];
        for (var i = 0; i < root.Zookeepers.Length; i++)
        {
            var keeperIndex = root.Zookeepers[i].Index;
            var targetIndex = PickZookeeperTarget(root, keeperIndex, myIndex, myViable, myCloaked);
            if (targetIndex < 0 || targetIndex == keeperIndex)
            {
                predicted[i] = keeperIndex;
                continue;
            }

            var targetDistances = GetDistances(root, targetIndex, distancesCache);
            var bestNext = keeperIndex;
            var bestDistance = targetDistances[keeperIndex];

            foreach (var action in MovementActions)
            {
                var next = GetNextIndex(root.Layout, keeperIndex, action);
                if (root.Walls[next])
                {
                    continue;
                }

                var nextDistance = targetDistances[next];
                if (nextDistance >= 0 && (bestDistance < 0 || nextDistance < bestDistance))
                {
                    bestDistance = nextDistance;
                    bestNext = next;
                }
            }

            predicted[i] = bestNext;
        }

        return predicted;
    }

    private static int PickZookeeperTarget(
        RootState root,
        int keeperIndex,
        int myIndex,
        bool myViable,
        bool myCloaked)
    {
        var targetIndex = -1;
        var bestDistance = int.MaxValue;
        var tie = false;
        var validCount = 0;

        if (myViable && !myCloaked)
        {
            validCount++;
            bestDistance = Manhattan(root.Layout, keeperIndex, myIndex);
            targetIndex = myIndex;
        }

        foreach (var opponent in root.Opponents)
        {
            if (!opponent.IsViable || opponent.CloakTicks > 0)
            {
                continue;
            }

            validCount++;
            var distance = Manhattan(root.Layout, keeperIndex, opponent.Index);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                targetIndex = opponent.Index;
                tie = false;
            }
            else if (distance == bestDistance)
            {
                tie = true;
            }
        }

        if (validCount <= 0)
        {
            return -1;
        }

        if (validCount == 1)
        {
            return targetIndex;
        }

        return tie ? -1 : targetIndex;
    }

    private static int CountExits(RootState root, int position)
    {
        var exits = 0;
        foreach (var action in MovementActions)
        {
            var next = GetNextIndex(root.Layout, position, action);
            if (!root.Walls[next])
            {
                exits++;
            }
        }

        return exits;
    }

    private static int[] GetDistances(RootState root, int startIndex, Dictionary<int, int[]> distancesCache)
    {
        if (distancesCache.TryGetValue(startIndex, out var cached))
        {
            return cached;
        }

        var distances = new int[root.Layout.CellCount];
        Array.Fill(distances, -1);
        var queue = new Queue<int>();
        distances[startIndex] = 0;
        queue.Enqueue(startIndex);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var nextDistance = distances[current] + 1;

            foreach (var action in MovementActions)
            {
                var next = GetNextIndex(root.Layout, current, action);
                if (root.Walls[next] || distances[next] >= 0)
                {
                    continue;
                }

                distances[next] = nextDistance;
                queue.Enqueue(next);
            }
        }

        distancesCache[startIndex] = distances;
        return distances;
    }

    private static double Simulate(RootState root, BotAction firstAction, ref FastRandom rng)
    {
        Span<ulong> consumed = stackalloc ulong[root.Layout.BitsetLength];
        Span<ZookeeperSim> zookeepers = root.Zookeepers.Length <= 4
            ? stackalloc ZookeeperSim[root.Zookeepers.Length]
            : new ZookeeperSim[root.Zookeepers.Length];

        for (var i = 0; i < root.Zookeepers.Length; i++)
        {
            zookeepers[i] = new ZookeeperSim(root.Zookeepers[i].Index, root.Zookeepers[i].TargetKind, root.Zookeepers[i].TargetSlot);
        }

        var position = root.MyIndex;
        var score = root.Score;
        var streak = root.ScoreStreak;
        var grace = 0;
        var activePower = root.ActivePower;
        var activeTicks = root.ActiveTicks;
        var heldPower = root.HeldPower;
        var postCaptureEscapeMode = position == root.SpawnIndex;
        var total = 0.0;
        var action = firstAction;

        for (var tick = 0; tick < SimulationHorizon; tick++)
        {
            var collectedPellet = false;

            if (action == BotAction.UseItem)
            {
                ActivateHeldPower(ref heldPower, ref activePower, ref activeTicks);
            }
            else
            {
                var next = GetNextIndex(root.Layout, position, action);
                if (next >= 0 && !root.Walls[next])
                {
                    position = next;
                }
            }

            if (position != root.SpawnIndex)
            {
                postCaptureEscapeMode = false;
            }

            CollectAtPosition(root, position, consumed, ref heldPower, activePower, ref score, ref collectedPellet);
            if (activePower == SimPowerType.Scavenger && activeTicks > 0)
            {
                ApplyScavenger(root, position, consumed, activePower, ref heldPower, ref score, ref collectedPellet);
            }

            if (collectedPellet)
            {
                streak++;
                grace = 0;
            }
            else
            {
                grace++;
                if (grace >= ScoreStreakResetGrace)
                {
                    streak = 0;
                }
            }

            if ((root.CurrentTick + tick) % ZookeeperRetargetInterval == 0)
            {
                RetargetZookeepers(root, zookeepers, position, false, activePower == SimPowerType.Cloak ? activeTicks : 0);
            }

            MoveZookeepers(root, zookeepers, position);

            if (IsCaptured(zookeepers, position))
            {
                var loss = Math.Max(PointsPerPellet, score * CaptureLossPercentage / 100);
                score -= loss;
                total -= loss * 1.35;
                position = root.SpawnIndex;
                streak = 0;
                grace = ScoreStreakResetGrace;
                postCaptureEscapeMode = true;
                activePower = SimPowerType.None;
                activeTicks = 0;
            }

            if (activeTicks > 0)
            {
                activeTicks--;
                if (activeTicks == 0)
                {
                    activePower = SimPowerType.None;
                }
            }

            total += EvaluateStep(root, position, score, streak, heldPower, activePower, activeTicks, consumed, zookeepers);
            action = ChooseRolloutAction(root, position, consumed, heldPower, activePower, activeTicks, postCaptureEscapeMode, zookeepers, action, ref rng);
        }

        return total + (score - root.Score);
    }

    private static void ActivateHeldPower(ref SimPowerType heldPower, ref SimPowerType activePower, ref int activeTicks)
    {
        if (heldPower == SimPowerType.None)
        {
            return;
        }

        activePower = heldPower;
        activeTicks = heldPower switch
        {
            SimPowerType.Cloak => CloakDuration,
            SimPowerType.Scavenger => ScavengerDuration,
            SimPowerType.BigMoose => BigMooseDuration,
            _ => 0,
        };
        heldPower = SimPowerType.None;
    }

    private static void CollectAtPosition(
        RootState root,
        int position,
        Span<ulong> consumed,
        ref SimPowerType heldPower,
        SimPowerType activePower,
        ref int score,
        ref bool collectedPellet)
    {
        if (IsConsumed(consumed, position))
        {
            return;
        }

        switch (root.Collectibles[position])
        {
            case CollectibleKind.Pellet:
                MarkConsumed(consumed, position);
                score += ScoreForPellet(PointsPerPellet, activePower);
                collectedPellet = true;
                break;
            case CollectibleKind.PowerPellet:
                MarkConsumed(consumed, position);
                score += ScoreForPellet(PointsPerPellet * PowerPelletMultiplier, activePower);
                collectedPellet = true;
                break;
            case CollectibleKind.Cloak:
                MarkConsumed(consumed, position);
                heldPower = SimPowerType.Cloak;
                break;
            case CollectibleKind.Scavenger:
                MarkConsumed(consumed, position);
                heldPower = SimPowerType.Scavenger;
                break;
            case CollectibleKind.BigMoose:
                MarkConsumed(consumed, position);
                heldPower = SimPowerType.BigMoose;
                break;
        }
    }

    private static void ApplyScavenger(
        RootState root,
        int position,
        Span<ulong> consumed,
        SimPowerType activePower,
        ref SimPowerType heldPower,
        ref int score,
        ref bool collectedPellet)
    {
        var centerX = root.Layout.XByIndex[position];
        var centerY = root.Layout.YByIndex[position];

        for (var i = 0; i < root.Layout.ScavengerOffsets.Length; i += 2)
        {
            var x = centerX + root.Layout.ScavengerOffsets[i];
            var y = centerY + root.Layout.ScavengerOffsets[i + 1];
            x = WrapCoord(x, root.Layout.Width);
            y = WrapCoord(y, root.Layout.Height);
            var index = ToIndex(root.Layout, x, y);
            if (IsConsumed(consumed, index))
            {
                continue;
            }

            switch (root.Collectibles[index])
            {
                case CollectibleKind.Pellet:
                    MarkConsumed(consumed, index);
                    score += ScoreForPellet(PointsPerPellet, activePower);
                    collectedPellet = true;
                    break;
                case CollectibleKind.PowerPellet:
                    MarkConsumed(consumed, index);
                    score += ScoreForPellet(PointsPerPellet * PowerPelletMultiplier, activePower);
                    collectedPellet = true;
                    break;
                case CollectibleKind.Cloak:
                    MarkConsumed(consumed, index);
                    heldPower = SimPowerType.Cloak;
                    break;
                case CollectibleKind.Scavenger:
                    MarkConsumed(consumed, index);
                    heldPower = SimPowerType.Scavenger;
                    break;
                case CollectibleKind.BigMoose:
                    MarkConsumed(consumed, index);
                    heldPower = SimPowerType.BigMoose;
                    break;
            }
        }
    }

    private static int ScoreForPellet(int baseValue, SimPowerType activePower)
    {
        return activePower == SimPowerType.BigMoose ? baseValue * BigMooseMultiplier : baseValue;
    }

    private static void RetargetZookeepers(RootState root, Span<ZookeeperSim> zookeepers, int myPosition, bool myInCage, int myCloakTicks)
    {
        for (var i = 0; i < zookeepers.Length; i++)
        {
            var keeper = zookeepers[i];
            var bestDistance = int.MaxValue;
            var tie = false;
            var targetKind = 0;
            var targetSlot = -1;

            if (!myInCage && myCloakTicks <= 0)
            {
                bestDistance = Manhattan(root.Layout, keeper.Index, myPosition);
                targetKind = 1;
            }

            for (var opponentIndex = 0; opponentIndex < root.Opponents.Length; opponentIndex++)
            {
                var opponent = root.Opponents[opponentIndex];
                if (opponent.Index == opponent.SpawnIndex || opponent.CloakTicks > 0)
                {
                    continue;
                }

                var distance = Manhattan(root.Layout, keeper.Index, opponent.Index);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    targetKind = 2;
                    targetSlot = opponentIndex;
                    tie = false;
                }
                else if (distance == bestDistance)
                {
                    tie = true;
                }
            }

            if (tie && keeper.TargetKind != targetKind)
            {
                targetKind = 0;
                targetSlot = -1;
            }

            zookeepers[i] = keeper with { TargetKind = targetKind, TargetSlot = targetSlot };
        }
    }

    private static void MoveZookeepers(RootState root, Span<ZookeeperSim> zookeepers, int myPosition)
    {
        for (var i = 0; i < zookeepers.Length; i++)
        {
            var keeper = zookeepers[i];
            var targetIndex = keeper.TargetKind switch
            {
                1 => myPosition,
                2 when keeper.TargetSlot >= 0 && keeper.TargetSlot < root.Opponents.Length => root.Opponents[keeper.TargetSlot].Index,
                _ => -1,
            };

            if (targetIndex < 0)
            {
                continue;
            }

            var bestIndex = keeper.Index;
            var bestDistance = Manhattan(root.Layout, keeper.Index, targetIndex);
            foreach (var action in MovementActions)
            {
                var next = GetNextIndex(root.Layout, keeper.Index, action);
                if (next >= 0 && !root.Walls[next])
                {
                    var distance = Manhattan(root.Layout, next, targetIndex);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestIndex = next;
                    }
                }
            }

            zookeepers[i] = keeper with { Index = bestIndex };
        }
    }

    private static bool IsCaptured(Span<ZookeeperSim> zookeepers, int myPosition)
    {
        for (var i = 0; i < zookeepers.Length; i++)
        {
            if (zookeepers[i].Index == myPosition)
            {
                return true;
            }
        }

        return false;
    }

    private static double EvaluateStep(
        RootState root,
        int position,
        int score,
        int streak,
        SimPowerType heldPower,
        SimPowerType activePower,
        int activeTicks,
        Span<ulong> consumed,
        Span<ZookeeperSim> zookeepers)
    {
        var nearestDanger = GetNearestDistance(root.Layout, zookeepers, position);
        var safety = nearestDanger switch
        {
            <= 1 => -240,
            2 => -120,
            3 => -60,
            4 => -24,
            _ => 8,
        };

        var heldValue = heldPower switch
        {
            SimPowerType.Cloak => 20,
            SimPowerType.Scavenger => 22,
            SimPowerType.BigMoose => 22,
            _ => 0,
        };

        var activeValue = activePower switch
        {
            SimPowerType.Cloak => activeTicks * 3,
            SimPowerType.Scavenger => activeTicks * 5,
            SimPowerType.BigMoose => activeTicks * 4,
            _ => 0,
        };

        var exitValue = CountExits(root, position) * 3;
        var localPotential = EstimateLocalPelletPotential(root, position, consumed);
        var potentialWeight = nearestDanger <= 2 ? 0.35 : 0.85;

        return safety
            + heldValue
            + activeValue
            + exitValue
            + Math.Min(24, streak * 4)
            + localPotential * potentialWeight
            + (score - root.Score) * 0.022;
    }

    private static BotAction ChooseRolloutAction(
        RootState root,
        int position,
        Span<ulong> consumed,
        SimPowerType heldPower,
        SimPowerType activePower,
        int activeTicks,
        bool inCage,
        Span<ZookeeperSim> zookeepers,
        BotAction previousAction,
        ref FastRandom rng)
    {
        var nearestDanger = GetNearestDistance(root.Layout, zookeepers, position);

        if (heldPower != SimPowerType.None)
        {
            var nearbyCollectibles = CountNearbyUnconsumed(root, position, consumed);
            if ((heldPower == SimPowerType.Cloak && nearestDanger <= 3)
                || (heldPower == SimPowerType.Scavenger && nearbyCollectibles >= 5)
                || (heldPower == SimPowerType.BigMoose && nearbyCollectibles >= 3))
            {
                return BotAction.UseItem;
            }
        }

        Span<BotAction> actions = stackalloc BotAction[4];
        Span<double> weights = stackalloc double[4];
        var count = 0;

        foreach (var action in MovementActions)
        {
            var next = GetNextIndex(root.Layout, position, action);
            if (next < 0 || root.Walls[next])
            {
                continue;
            }

            var nextDanger = GetNearestDistance(root.Layout, zookeepers, next);
            var exitCount = CountExits(root, next);
            var localPotential = EstimateLocalPelletPotential(root, next, consumed);
            var weight = 1.0 + nextDanger * 1.5;
            if (root.Collectibles[next] == CollectibleKind.Pellet)
            {
                weight += activePower == SimPowerType.BigMoose ? 18 : 10;
            }
            else if (root.Collectibles[next] == CollectibleKind.PowerPellet)
            {
                weight += activePower == SimPowerType.BigMoose ? 42 : 24;
            }
            else if (root.Collectibles[next] >= CollectibleKind.Cloak && heldPower == SimPowerType.None)
            {
                weight += 14;
            }

            weight += exitCount * (nextDanger <= 2 ? 2.5 : 1.5);
            weight += localPotential * (nextDanger <= 2 ? 0.28 : 0.65);

            if (nextDanger <= 1)
            {
                weight *= 0.12;
            }
            else if (nextDanger <= 2 && exitCount <= 2)
            {
                weight *= 0.55;
            }

            if (IsOpposite(previousAction, action))
            {
                weight *= 0.45;
            }

            if (inCage && next != root.SpawnIndex)
            {
                weight += 18;
            }

            if (activePower == SimPowerType.Cloak && activeTicks > 0)
            {
                weight += 4;
            }

            actions[count] = action;
            weights[count] = weight;
            count++;
        }

        if (count == 0)
        {
            return BotAction.Up;
        }

        var totalWeight = 0.0;
        for (var i = 0; i < count; i++)
        {
            totalWeight += weights[i];
        }

        var sample = rng.NextDouble() * totalWeight;
        for (var i = 0; i < count; i++)
        {
            sample -= weights[i];
            if (sample <= 0)
            {
                return actions[i];
            }
        }

        return actions[count - 1];
    }

    private static double EstimateLocalPelletPotential(RootState root, int centerIndex, Span<ulong> consumed)
    {
        var centerX = root.Layout.XByIndex[centerIndex];
        var centerY = root.Layout.YByIndex[centerIndex];
        var total = 0.0;

        for (var dy = -LocalPotentialRadius; dy <= LocalPotentialRadius; dy++)
        {
            for (var dx = -LocalPotentialRadius; dx <= LocalPotentialRadius; dx++)
            {
                var distance = Math.Abs(dx) + Math.Abs(dy);
                if (distance <= 0 || distance > LocalPotentialRadius)
                {
                    continue;
                }

                var x = WrapCoord(centerX + dx, root.Layout.Width);
                var y = WrapCoord(centerY + dy, root.Layout.Height);
                var index = ToIndex(root.Layout, x, y);
                if (root.Walls[index] || IsConsumed(consumed, index))
                {
                    continue;
                }

                total += root.Collectibles[index] switch
                {
                    CollectibleKind.Pellet => 8.0 / distance,
                    CollectibleKind.PowerPellet => 32.0 / distance,
                    _ => 0.0,
                };
            }
        }

        return total;
    }

    private static int CountNearbyUnconsumed(RootState root, int centerIndex, Span<ulong> consumed)
    {
        var centerX = root.Layout.XByIndex[centerIndex];
        var centerY = root.Layout.YByIndex[centerIndex];
        var count = 0;

        for (var dy = -2; dy <= 2; dy++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                var x = centerX + dx;
                var y = centerY + dy;
                x = WrapCoord(x, root.Layout.Width);
                y = WrapCoord(y, root.Layout.Height);
                var index = ToIndex(root.Layout, x, y);
                if (!IsConsumed(consumed, index) && root.Collectibles[index] is CollectibleKind.Pellet or CollectibleKind.PowerPellet)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int GetNearestDistance(LayoutCache layout, Span<ZookeeperSim> zookeepers, int position)
    {
        var best = int.MaxValue;
        for (var i = 0; i < zookeepers.Length; i++)
        {
            best = Math.Min(best, Manhattan(layout, zookeepers[i].Index, position));
        }

        return best == int.MaxValue ? 10 : best;
    }

    private static int GetNearestRootDangerDistance(RootState root, int position)
    {
        var best = int.MaxValue;
        for (var i = 0; i < root.Zookeepers.Length; i++)
        {
            best = Math.Min(best, Manhattan(root.Layout, root.Zookeepers[i].Index, position));
        }

        return best == int.MaxValue ? 10 : best;
    }

    private static int Manhattan(LayoutCache layout, int leftIndex, int rightIndex)
    {
        var dx = Math.Abs(layout.XByIndex[leftIndex] - layout.XByIndex[rightIndex]);
        var dy = Math.Abs(layout.YByIndex[leftIndex] - layout.YByIndex[rightIndex]);
        dx = Math.Min(dx, layout.Width - dx);
        dy = Math.Min(dy, layout.Height - dy);
        return dx + dy;
    }

    private static int WrapCoord(int value, int size)
    {
        var wrapped = value % size;
        return wrapped < 0 ? wrapped + size : wrapped;
    }

    private static int GetNextIndex(LayoutCache layout, int currentIndex, BotAction action)
    {
        return action switch
        {
            BotAction.Up => layout.Up[currentIndex],
            BotAction.Down => layout.Down[currentIndex],
            BotAction.Left => layout.Left[currentIndex],
            BotAction.Right => layout.Right[currentIndex],
            _ => currentIndex,
        };
    }

    private static bool IsOpposite(BotAction previous, BotAction next)
    {
        return (previous == BotAction.Up && next == BotAction.Down)
            || (previous == BotAction.Down && next == BotAction.Up)
            || (previous == BotAction.Left && next == BotAction.Right)
            || (previous == BotAction.Right && next == BotAction.Left);
    }

    private static int ToIndex(LayoutCache layout, int x, int y)
    {
        return y * layout.Width + x;
    }

    private static bool IsConsumed(Span<ulong> consumed, int index)
    {
        var bucket = index >> 6;
        return (consumed[bucket] & (1UL << (index & 63))) != 0;
    }

    private static void MarkConsumed(Span<ulong> consumed, int index)
    {
        var bucket = index >> 6;
        consumed[bucket] |= 1UL << (index & 63);
    }

    private static SimPowerType ToPowerState(PowerUpType? type)
    {
        return type switch
        {
            PowerUpType.ChameleonCloak => SimPowerType.Cloak,
            PowerUpType.Scavenger => SimPowerType.Scavenger,
            PowerUpType.BigMooseJuice => SimPowerType.BigMoose,
            _ => SimPowerType.None,
        };
    }

    private static uint HashSeed(int tick, int x, int y, int score)
    {
        var hash = unchecked((uint)tick * 397u);
        hash = unchecked(hash * 397u + (uint)(x * 31 + y));
        hash = unchecked(hash * 397u + (uint)score);
        return hash == 0 ? 1u : hash;
    }

    private static class CollectibleKind
    {
        public const byte None = 0;
        public const byte Pellet = 1;
        public const byte PowerPellet = 2;
        public const byte Cloak = 3;
        public const byte Scavenger = 4;
        public const byte BigMoose = 5;
    }

    private enum SimPowerType : byte
    {
        None = 0,
        Cloak = 1,
        Scavenger = 2,
        BigMoose = 3,
    }

    private readonly record struct RootState(
        LayoutCache Layout,
        bool[] Walls,
        byte[] Collectibles,
        BotAction[] Candidates,
        int CandidateCount,
        int CurrentTick,
        int MyIndex,
        int SpawnIndex,
        bool MyViable,
        int Score,
        int ScoreStreak,
        SimPowerType ActivePower,
        int ActiveTicks,
        SimPowerType HeldPower,
        ZookeeperState[] Zookeepers,
        OpponentState[] Opponents);

    private sealed class LayoutCache
    {
        public LayoutCache(int width, int height, int[] xByIndex, int[] yByIndex, int[] up, int[] down, int[] left, int[] right, int[] scavengerOffsets)
        {
            Width = width;
            Height = height;
            XByIndex = xByIndex;
            YByIndex = yByIndex;
            Up = up;
            Down = down;
            Left = left;
            Right = right;
            ScavengerOffsets = scavengerOffsets;
            CellCount = width * height;
            BitsetLength = (CellCount + 63) >> 6;
        }

        public int Width { get; }
        public int Height { get; }
        public int CellCount { get; }
        public int BitsetLength { get; }
        public int[] XByIndex { get; }
        public int[] YByIndex { get; }
        public int[] Up { get; }
        public int[] Down { get; }
        public int[] Left { get; }
        public int[] Right { get; }
        public int[] ScavengerOffsets { get; }
    }

    private readonly record struct OpponentState(int Index, int SpawnIndex, bool IsViable, int CloakTicks);
    private readonly record struct ZookeeperState(int Index, int TargetKind, int TargetSlot);
    private readonly record struct ZookeeperSim(int Index, int TargetKind, int TargetSlot);
    private readonly record struct SafetyEvaluation(
        BotAction Action,
        bool IsValid,
        bool FatalNow,
        bool FatalAfterMove,
        int NearestAfterMove,
        int ExitCount,
        int CollectibleScore,
        bool IsFallback,
        int SpawnDistance);

    private struct FastRandom
    {
        private uint _state;

        public FastRandom(uint seed)
        {
            _state = seed == 0 ? 1u : seed;
        }

        public uint NextUInt()
        {
            var x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        public double NextDouble()
        {
            return (NextUInt() & 0x00FFFFFF) / (double)0x01000000;
        }
    }
}
