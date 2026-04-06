using ClingyHeuroBot2.Services;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using MonteCarloBot.Services;
using Serilog;
using Serilog.Core;

namespace NeuralNetBot.Services;

public sealed class NeuralNetBotService : IBot<NeuralNetBotService>
{
    private const int SimulationHorizon = 8;
    private const int EstimatedPelletScore = 64;
    private const int EstimatedPowerPelletScore = EstimatedPelletScore * 10;
    private const int PowerPelletDuration = 12;
    private const int CloakDuration = 20;
    private const int ScavengerDuration = 5;
    private const int BigMooseDuration = 5;
    private const int PostCaptureEscapeTicks = 12;
    private const int PostCaptureSpawnRadius = 6;
    private const int EmergencyDangerDistance = 3;
    private const int RecentPositionWindow = 10;

    private static readonly BotAction[] MovementActions =
    [
        BotAction.Up,
        BotAction.Down,
        BotAction.Left,
        BotAction.Right,
    ];

    private static readonly double[][] HiddenWeights =
    [
        [1.25, 0.75, 0.80, 1.35, 1.50, 1.10, 1.20, -1.20, 0.60, -0.50, 0.20, 0.15, 0.30, 1.10, -1.40, 0.90],
        [0.15, 0.25, 0.35, 2.10, 2.30, 0.45, 1.55, -1.80, 0.90, -0.40, 0.10, 0.10, 0.25, 1.95, -2.10, 0.55],
        [1.40, 1.55, 1.35, 0.25, 0.45, 1.80, 0.80, -0.35, 0.25, -0.20, 0.30, 0.20, 0.65, 0.15, -0.25, 1.60],
        [0.20, 0.20, 0.25, 0.60, 0.85, 0.75, 0.65, -0.60, 0.25, -0.15, 1.30, 1.15, 0.40, 0.65, -0.75, 0.55],
        [0.10, 0.15, 0.20, 0.80, 1.25, 0.40, 1.10, -1.35, 1.35, -0.30, -0.05, 0.10, 1.75, 1.30, -1.10, 1.45],
        [0.95, 0.75, 0.70, 1.00, 1.40, 1.25, 0.90, -0.95, 0.55, -0.75, 0.05, 0.25, 0.35, 1.60, -1.55, 0.70],
        [0.65, 0.55, 0.60, 0.75, 1.10, 1.50, 0.70, -0.40, 0.15, -0.35, 0.20, 0.10, 0.45, 0.70, -0.60, 1.85],
        [0.05, 0.10, 0.15, 1.10, 1.85, 0.20, 1.35, -1.65, 1.55, -0.10, 0.05, 0.05, 0.40, 2.15, -2.30, 0.35],
    ];

    private static readonly double[] HiddenBiases = [0.10, -0.20, -0.05, -0.10, -0.25, 0.05, 0.10, -0.20];
    private static readonly double[] OutputWeights = [1.05, 1.35, 1.10, 0.55, 0.90, 1.00, 0.90, 1.20];
    private const double OutputBias = -0.20;

    private readonly ILogger _logger;
    private MonteCarloBotService _monteCarloBot;
    private HeuroBotService _clingyBot;

    private (int X, int Y)? _lastPosition;
    private (int X, int Y)? _previousPosition;
    private int _lastCapturedCounter = -1;
    private int _postCaptureEscapeTicksRemaining;
    private readonly Queue<(int X, int Y)> _recentPositions = new();

    public NeuralNetBotService(ILogger? logger = null)
    {
        _logger = logger ?? Logger.None;
        _monteCarloBot = new MonteCarloBotService(_logger);
        _clingyBot = new HeuroBotService(_logger);
    }

    public Guid BotId { get; set; }

    public void SetBotId(Guid botId)
    {
        if (BotId != botId)
        {
            ResetSessionState(recreateExperts: true);
        }

        BotId = botId;
        _monteCarloBot.SetBotId(botId);
        _clingyBot.BotId = botId;
    }

    public void ResetSession()
    {
        BotId = Guid.Empty;
        ResetSessionState(recreateExperts: true);
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

        ResetMovementMemoryIfCaptured(me);

        const BotAction fallbackAction = BotAction.Up;
        var monteAction = TryGetExpertAction(gameState, _monteCarloBot.GetAction, fallbackAction);
        var clingyAction = TryGetExpertAction(gameState, _clingyBot.GetAction, fallbackAction);

        var geometry = GetBoardGeometry(gameState.Cells);
        var board = gameState.Cells.ToDictionary(cell => (cell.X, cell.Y));
        var legalMoves = MovementActions
            .Where(action => IsLegalMove(board, geometry, me.X, me.Y, action))
            .ToList();

        if (legalMoves.Count == 0)
        {
            return me.HeldPowerUp.HasValue ? BotAction.UseItem : BotAction.Up;
        }

        var candidates = new List<BotAction>(legalMoves);
        if (me.HeldPowerUp.HasValue)
        {
            candidates.Add(BotAction.UseItem);
        }

        var bestAction = legalMoves.Contains(monteAction)
            ? monteAction
            : legalMoves.Contains(clingyAction)
                ? clingyAction
                : legalMoves[0];
        var bestScore = double.NegativeInfinity;
        var currentDanger = GetNearestZookeeperDistance(me.X, me.Y, gameState.Zookeepers, geometry);
        var isProtected = IsProtected(me.ActivePowerUp?.Type, me.ActivePowerUp?.TicksRemaining ?? 0);
        var protectionWeight = GetProtectionWeight(gameState, me);
        var usePostCaptureEscape = ShouldUsePostCaptureEscape(me, geometry, currentDanger);
        var bestOpponentScore = gameState.Animals
            .Where(a => a.Id != me.Id)
            .Select(a => a.Score)
            .DefaultIfEmpty(me.Score)
            .Max();
        var scoreDeficit = Math.Max(0, bestOpponentScore - me.Score);
        var lateGame = gameState.Tick >= 120;

        if (!isProtected && (currentDanger <= EmergencyDangerDistance || usePostCaptureEscape))
        {
            var emergencyAction = SelectEmergencyAction(
                gameState,
                board,
                geometry,
                me,
                legalMoves,
                monteAction,
                clingyAction,
                currentDanger,
                usePostCaptureEscape);

            RememberPosition(me.X, me.Y);
            return emergencyAction;
        }

        if (legalMoves.Contains(monteAction)
            && monteAction != BotAction.UseItem
            && ShouldFollowMonteBaseline(gameState, board, geometry, me, legalMoves, monteAction, currentDanger, scoreDeficit, gameState.Tick))
        {
            RememberPosition(me.X, me.Y);
            return monteAction;
        }

        var actionScores = new Dictionary<BotAction, double>();
        foreach (var candidate in candidates)
        {
            var score = ScoreCandidate(
                gameState,
                board,
                geometry,
                me,
                candidate,
                monteAction,
                clingyAction,
                currentDanger,
                isProtected,
                protectionWeight,
                scoreDeficit,
                lateGame);
            actionScores[candidate] = score;

            if (score > bestScore)
            {
                bestScore = score;
                bestAction = candidate;
            }
        }

        if (!isProtected
            && legalMoves.Contains(monteAction)
            && bestAction != BotAction.UseItem
            && bestAction != monteAction
            && actionScores.TryGetValue(monteAction, out var monteScore)
            && ShouldPreferMonteOverride(
                gameState,
                board,
                geometry,
                me,
                bestAction,
                monteAction,
                bestScore,
                monteScore,
                currentDanger,
                scoreDeficit,
                gameState.Tick))
        {
            bestAction = monteAction;
        }

        RememberPosition(me.X, me.Y);

        return bestAction;
    }

    private static bool IsLegalMove(
        IReadOnlyDictionary<(int X, int Y), Cell> board,
        BoardGeometry geometry,
        int currentX,
        int currentY,
        BotAction action)
    {
        var (nextX, nextY) = ApplyAction(geometry, currentX, currentY, action);
        return board.TryGetValue((nextX, nextY), out var cell) && cell.Content != CellContent.Wall;
    }

    private double ScoreCandidate(
        GameState gameState,
        IReadOnlyDictionary<(int X, int Y), Cell> board,
        BoardGeometry geometry,
        Animal me,
        BotAction candidate,
        BotAction monteAction,
        BotAction clingyAction,
        int currentDanger,
        bool isProtected,
        double protectionWeight,
        int scoreDeficit,
        bool lateGame)
    {
        var useItemFit = 0.0;
        var immediateValue = 0.0;
        var densityScore = 0.0;
        var nearestCollectibleScore = 0.0;
        var dangerNowScore = 0.0;
        var closeThreatScore = 0.0;
        var mobilityScore = 0.0;
        var reversePenalty = 0.0;
        var revisitPenalty = 0.0;
        var safeAreaScore = 0.0;
        var dangerProgressScore = 0.0;

        if (candidate == BotAction.UseItem)
        {
            useItemFit = ScoreUseItemFit(me, currentDanger, gameState, board, geometry);
            dangerNowScore = NormalizeDanger(currentDanger);
            closeThreatScore = Math.Clamp(CountThreatsWithin(me.X, me.Y, gameState.Zookeepers, 3, geometry) / 3.0, 0.0, 1.0);
            mobilityScore = CountLegalNeighbors(board, geometry, me.X, me.Y) / 4.0;
            safeAreaScore = ScoreReachableSafeArea(board, geometry, me.X, me.Y, gameState.Zookeepers, isProtected);
        }
        else
        {
            var (nextX, nextY) = ApplyAction(geometry, me.X, me.Y, candidate);
            var nextCell = board[(nextX, nextY)];
            var nextDanger = GetNearestZookeeperDistance(nextX, nextY, gameState.Zookeepers, geometry);
            var nearbyThreats = CountThreatsWithin(nextX, nextY, gameState.Zookeepers, 3, geometry);
            immediateValue = NormalizeReward(GetCollectibleReward(nextCell.Content, me.ActivePowerUp?.Type));
            densityScore = Math.Clamp(GetCollectibleDensity(board.Values, nextX, nextY, 4, geometry), 0.0, 1.0);
            nearestCollectibleScore = Math.Clamp(FindBestReachableCollectible(board, geometry, nextX, nextY, 10), 0.0, 1.0);
            dangerNowScore = NormalizeDanger(nextDanger);
            closeThreatScore = Math.Clamp(nearbyThreats / 3.0, 0.0, 1.0);
            mobilityScore = CountLegalNeighbors(board, geometry, nextX, nextY) / 4.0;
            reversePenalty = _previousPosition is { } previous && previous.X == nextX && previous.Y == nextY ? 1.0 : 0.0;
            revisitPenalty = Math.Clamp(GetRecentVisitCount(nextX, nextY) / 3.0, 0.0, 1.0);
            safeAreaScore = ScoreReachableSafeArea(board, geometry, nextX, nextY, gameState.Zookeepers, isProtected);
            dangerProgressScore = Math.Clamp((nextDanger - currentDanger) / 3.0, -1.0, 1.0);
            if (!isProtected && nextDanger <= 0)
            {
                return -5000.0;
            }
        }

        var outlook = SimulateOutlook(gameState, board, geometry, me, candidate);
        var safetyFloorScore = NormalizeDanger(outlook.MinDanger);
        var projectedRewardScore = NormalizeReward(outlook.ProjectedReward, 1500.0);
        var safePathScore = Math.Clamp(outlook.SafePathRatio, 0.0, 1.0);
        var trapPenalty = Math.Clamp(outlook.TrapPenalty, 0.0, 1.0);
        var captureRisk = Math.Clamp(outlook.CaptureRisk, 0.0, 1.0);
        var powerMomentum = Math.Clamp(outlook.PowerMomentum, 0.0, 1.0);

        var features = new[]
        {
            immediateValue,
            densityScore,
            nearestCollectibleScore,
            dangerNowScore,
            safetyFloorScore,
            projectedRewardScore,
            safePathScore,
            trapPenalty,
            mobilityScore,
            reversePenalty,
            candidate == monteAction ? 1.0 : 0.0,
            candidate == clingyAction ? 1.0 : 0.0,
            useItemFit,
            Math.Clamp(protectionWeight / 3.0, 0.0, 1.0),
            captureRisk,
            powerMomentum,
        };

        var score = EvaluateNetwork(features)
            + projectedRewardScore * 1.60
            + safePathScore * 1.15
            + dangerNowScore * (0.40 + protectionWeight * 0.55)
            + safetyFloorScore * (0.65 + protectionWeight * 0.80)
            - trapPenalty * (0.50 + protectionWeight * 0.45)
            - revisitPenalty * (0.55 + protectionWeight * 0.35)
            - captureRisk * (1.40 + protectionWeight * 1.20)
            - closeThreatScore * Math.Max(0.0, protectionWeight - 0.40);

        if (candidate != BotAction.UseItem && candidate == clingyAction)
        {
            score += 0.22;
        }

        if (candidate != BotAction.UseItem && candidate == monteAction)
        {
            score += 0.82;
            if (lateGame)
            {
                score += 0.55;
            }

            if (scoreDeficit > 0)
            {
                score += 0.18 + Math.Min(0.72, scoreDeficit / 2600.0);
            }

            if (scoreDeficit >= 4000)
            {
                score += 0.35;
            }

            if (!isProtected && currentDanger <= EmergencyDangerDistance)
            {
                score += 0.60;
            }
        }

        if (candidate != BotAction.UseItem && candidate == monteAction && candidate == clingyAction)
        {
            score += 0.38;
        }

        if (candidate != BotAction.UseItem && candidate != monteAction)
        {
            if (lateGame)
            {
                score -= 0.40;
            }

            if (scoreDeficit > 0)
            {
                score -= 0.16 + Math.Min(0.60, scoreDeficit / 3000.0);
            }

            if (scoreDeficit >= 4000)
            {
                score -= 0.30;
            }

            if (!isProtected && dangerNowScore <= 0.35)
            {
                score -= 0.95;
            }
            else if (!isProtected && safetyFloorScore <= 0.45)
            {
                score -= 0.45;
            }

            if (currentDanger >= 4 && immediateValue <= 0.15 && projectedRewardScore <= 0.35)
            {
                score -= 0.55;
            }
        }

        if (candidate == BotAction.UseItem && me.HeldPowerUp is PowerUpType.PowerPellet or PowerUpType.ChameleonCloak)
        {
            score += 0.50 + protectionWeight * 0.25;
        }

        if (candidate != BotAction.UseItem && mobilityScore <= 0.25)
        {
            score -= 0.60 + protectionWeight * 0.15;
        }

        if (!isProtected && candidate != BotAction.UseItem && currentDanger <= 3)
        {
            score += safeAreaScore * (0.40 + protectionWeight * 0.35);
            score += dangerProgressScore * (0.18 + protectionWeight * 0.22);
        }

        if (!isProtected && candidate != BotAction.UseItem && currentDanger <= 3 && safeAreaScore <= 0.20)
        {
            score -= 0.80 + protectionWeight * 0.30;
        }

        if (!isProtected && candidate != BotAction.UseItem && revisitPenalty >= 0.66)
        {
            score -= 0.75 + protectionWeight * 0.20;
        }

        if (!isProtected && candidate != BotAction.UseItem && currentDanger <= 2 && dangerProgressScore < 0.0)
        {
            score -= 0.55 + protectionWeight * 0.20;
        }

        if (!isProtected && candidate != BotAction.UseItem && dangerNowScore <= 0.20 && mobilityScore <= 0.50)
        {
            score -= 0.85 + protectionWeight * 0.30;
        }

        if (!isProtected && currentDanger <= 2 && candidate != BotAction.UseItem)
        {
            score += dangerNowScore * 0.75;
        }

        if (outlook.WasCaptured)
        {
            score -= 2.80 + protectionWeight * 2.10;
        }

        return score;
    }

    private BotAction SelectEmergencyAction(
        GameState gameState,
        IReadOnlyDictionary<(int X, int Y), Cell> board,
        BoardGeometry geometry,
        Animal me,
        IReadOnlyList<BotAction> legalMoves,
        BotAction monteAction,
        BotAction clingyAction,
        int currentDanger,
        bool usePostCaptureEscape)
    {
        var bestAction = legalMoves[0];
        var bestScore = double.NegativeInfinity;
        var spawnDistanceNow = GetWrappedDistance(geometry, me.X, me.Y, me.SpawnX, me.SpawnY);

        foreach (var candidate in legalMoves)
        {
            var (nextX, nextY) = ApplyAction(geometry, me.X, me.Y, candidate);
            var nextDanger = GetNearestZookeeperDistance(nextX, nextY, gameState.Zookeepers, geometry);
            if (nextDanger <= 0)
            {
                continue;
            }

            var exits = CountLegalNeighbors(board, geometry, nextX, nextY);
            var safeArea = ScoreReachableSafeArea(board, geometry, nextX, nextY, gameState.Zookeepers, false);
            var dangerProgress = nextDanger - currentDanger;
            var collectibleReward = NormalizeReward(GetCollectibleReward(board[(nextX, nextY)].Content, me.ActivePowerUp?.Type), 300.0);
            var revisitPenalty = Math.Min(GetRecentVisitCount(nextX, nextY), 3);
            var spawnDistance = GetWrappedDistance(geometry, nextX, nextY, me.SpawnX, me.SpawnY);

            var score = nextDanger * 3.10
                + exits * 0.90
                + safeArea * 2.60
                + dangerProgress * 1.35
                + collectibleReward * 0.55
                - revisitPenalty * 1.40;

            if (nextDanger == 1)
            {
                score -= 3.00;
            }

            if (exits <= 1)
            {
                score -= 2.20;
            }

            if (candidate == monteAction)
            {
                score += 0.95;
            }

            if (candidate == clingyAction)
            {
                score += 0.30;
            }

            if (usePostCaptureEscape)
            {
                score += Math.Max(0, spawnDistance - spawnDistanceNow) * 0.85;
                score += spawnDistance * 0.22;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestAction = candidate;
            }
        }

        if (me.HeldPowerUp.HasValue)
        {
            var useItemScore = ScoreEmergencyUseItem(me.HeldPowerUp.Value, currentDanger, usePostCaptureEscape);
            if (useItemScore > bestScore)
            {
                return BotAction.UseItem;
            }
        }

        return bestAction;
    }

    private static double ScoreEmergencyUseItem(PowerUpType heldPower, int currentDanger, bool usePostCaptureEscape)
    {
        return heldPower switch
        {
            PowerUpType.PowerPellet => currentDanger <= 3 ? 10.0 : (usePostCaptureEscape ? 7.0 : double.NegativeInfinity),
            PowerUpType.ChameleonCloak => currentDanger <= 3 ? 9.5 : (usePostCaptureEscape ? 7.5 : double.NegativeInfinity),
            PowerUpType.Scavenger => usePostCaptureEscape ? -5.0 : double.NegativeInfinity,
            PowerUpType.BigMooseJuice => usePostCaptureEscape ? -4.0 : double.NegativeInfinity,
            _ => double.NegativeInfinity,
        };
    }

    private static bool ShouldPreferMonteOverride(
        GameState gameState,
        IReadOnlyDictionary<(int X, int Y), Cell> board,
        BoardGeometry geometry,
        Animal me,
        BotAction bestAction,
        BotAction monteAction,
        double bestScore,
        double monteScore,
        int currentDanger,
        int scoreDeficit,
        int tick)
    {
        var bestProfile = CaptureMoveProfile(gameState, board, geometry, me, bestAction);
        var monteProfile = CaptureMoveProfile(gameState, board, geometry, me, monteAction);
        var hardChaseBias = scoreDeficit >= 4000;
        var monteBias = currentDanger >= 3 || scoreDeficit > 0 || tick >= 120;

        if (monteProfile.NextDanger <= 0)
        {
            return false;
        }

        if (bestProfile.WasCaptured && !monteProfile.WasCaptured)
        {
            return true;
        }

        var monteSafer = monteProfile.NextDanger > bestProfile.NextDanger
            || monteProfile.SafeArea > bestProfile.SafeArea + 0.08
            || monteProfile.Mobility > bestProfile.Mobility
            || monteProfile.SafePathRatio > bestProfile.SafePathRatio + 0.08
            || monteProfile.CaptureRisk + 0.05 < bestProfile.CaptureRisk;

        var bestClearlyBetterReward = (bestProfile.ImmediateReward > monteProfile.ImmediateReward + (hardChaseBias ? 520.0 : monteBias ? 420.0 : 224.0)
                || bestProfile.ProjectedReward > monteProfile.ProjectedReward + (hardChaseBias ? 860.0 : monteBias ? 680.0 : 320.0)
                || bestScore > monteScore + (hardChaseBias ? 3.00 : monteBias ? 2.45 : 1.40))
            && bestProfile.CaptureRisk <= monteProfile.CaptureRisk + 0.03
            && bestProfile.SafePathRatio + 0.05 >= monteProfile.SafePathRatio;

        if ((currentDanger >= 4 || monteBias) && !bestClearlyBetterReward)
        {
            return true;
        }

        if (monteSafer && !bestClearlyBetterReward)
        {
            return true;
        }

        return monteSafer && monteScore >= bestScore - 0.85;
    }
    private static bool ShouldFollowMonteBaseline(
        GameState gameState,
        IReadOnlyDictionary<(int X, int Y), Cell> board,
        BoardGeometry geometry,
        Animal me,
        IReadOnlyList<BotAction> legalMoves,
        BotAction monteAction,
        int currentDanger,
        int scoreDeficit,
        int tick)
    {
        if (currentDanger <= EmergencyDangerDistance)
        {
            return false;
        }

        if (me.HeldPowerUp.HasValue && ScoreUseItemFit(me, currentDanger, gameState, board, geometry) >= 0.85)
        {
            return false;
        }

        var monteProfile = CaptureMoveProfile(gameState, board, geometry, me, monteAction);
        var hardChaseBias = scoreDeficit >= 4000;
        var monteBias = currentDanger >= 3 || scoreDeficit > 0 || tick >= 120;
        if (monteProfile.NextDanger <= 0)
        {
            return false;
        }

        foreach (var action in legalMoves)
        {
            if (action == monteAction)
            {
                continue;
            }

            var profile = CaptureMoveProfile(gameState, board, geometry, me, action);
            if (profile.WasCaptured && !monteProfile.WasCaptured)
            {
                continue;
            }

            var saferOrEqual = profile.NextDanger >= monteProfile.NextDanger
                && profile.SafeArea + 0.05 >= monteProfile.SafeArea
                && profile.CaptureRisk <= monteProfile.CaptureRisk + 0.03
                && profile.SafePathRatio + 0.05 >= monteProfile.SafePathRatio;

            var materiallyBetterReward = profile.ImmediateReward > monteProfile.ImmediateReward + (hardChaseBias ? 460.0 : monteBias ? 360.0 : 160.0)
                || (profile.ImmediateReward >= EstimatedPowerPelletScore && monteProfile.ImmediateReward < EstimatedPowerPelletScore)
                || (profile.ProjectedReward > monteProfile.ProjectedReward + (hardChaseBias ? 920.0 : monteBias ? 720.0 : 340.0) && saferOrEqual);

            var materiallySafer = profile.NextDanger > monteProfile.NextDanger
                && profile.SafeArea >= monteProfile.SafeArea + 0.12
                && profile.CaptureRisk + 0.06 < monteProfile.CaptureRisk
                && profile.ProjectedReward + 180.0 >= monteProfile.ProjectedReward;

            if ((materiallyBetterReward || materiallySafer) && saferOrEqual)
            {
                return false;
            }
        }

        if (monteBias && monteProfile.CaptureRisk <= 0.22 && monteProfile.SafePathRatio >= 0.60)
        {
            return true;
        }

        return true;
    }

    private static MoveProfile CaptureMoveProfile(
        GameState gameState,
        IReadOnlyDictionary<(int X, int Y), Cell> board,
        BoardGeometry geometry,
        Animal me,
        BotAction action)
    {
        var (nextX, nextY) = ApplyAction(geometry, me.X, me.Y, action);
        var nextCell = board[(nextX, nextY)];
        var outlook = SimulateOutlook(gameState, board, geometry, me, action);

        return new MoveProfile(
            NextDanger: GetNearestZookeeperDistance(nextX, nextY, gameState.Zookeepers, geometry),
            SafeArea: ScoreReachableSafeArea(board, geometry, nextX, nextY, gameState.Zookeepers, false),
            Mobility: CountLegalNeighbors(board, geometry, nextX, nextY),
            ImmediateReward: GetCollectibleReward(nextCell.Content, me.ActivePowerUp?.Type),
            ProjectedReward: outlook.ProjectedReward,
            CaptureRisk: outlook.CaptureRisk,
            SafePathRatio: outlook.SafePathRatio,
            WasCaptured: outlook.WasCaptured);
    }

    private static OutlookMetrics SimulateOutlook(
        GameState gameState,
        IReadOnlyDictionary<(int X, int Y), Cell> board,
        BoardGeometry geometry,
        Animal me,
        BotAction firstAction)
    {
        var consumed = new HashSet<(int X, int Y)>();
        IReadOnlyList<ZookeeperPosition> zookeepers = gameState.Zookeepers
            .Select(z => new ZookeeperPosition(z.X, z.Y))
            .ToArray();

        var x = me.X;
        var y = me.Y;
        var previousAction = BotAction.Up;
        var hasPreviousAction = false;
        var heldPower = me.HeldPowerUp;
        var activePower = me.ActivePowerUp?.Type;
        var activeTicks = me.ActivePowerUp?.TicksRemaining ?? 0;
        var projectedReward = 0.0;
        var minDanger = GetNearestZookeeperDistance(x, y, zookeepers, geometry);
        var safeSteps = 0;
        var trapSteps = 0;
        var captureRisk = 0.0;
        var powerMomentum = 0.0;
        var action = firstAction;
        var wasCaptured = false;

        for (var step = 0; step < SimulationHorizon; step++)
        {
            if (action == BotAction.UseItem)
            {
                if (ActivateHeldPower(ref heldPower, ref activePower, ref activeTicks))
                {
                    powerMomentum += 0.35;
                }
            }
            else if (IsLegalMove(board, geometry, x, y, action))
            {
                (x, y) = ApplyAction(geometry, x, y, action);
            }

            projectedReward += CollectAtPosition(board, consumed, x, y, ref heldPower, activePower);

            zookeepers = MoveSimulatedZookeepers(board, geometry, zookeepers, x, y, IsProtected(activePower, activeTicks));
            var danger = GetNearestZookeeperDistance(x, y, zookeepers, geometry);
            minDanger = Math.Min(minDanger, danger);

            var mobility = CountLegalNeighbors(board, geometry, x, y);
            if (danger >= 3 && mobility >= 2)
            {
                safeSteps++;
            }

            if (mobility <= 1)
            {
                trapSteps++;
            }

            if (!IsProtected(activePower, activeTicks))
            {
                captureRisk += danger switch
                {
                    <= 0 => 1.0,
                    1 => 0.70,
                    2 => 0.30,
                    3 => 0.10,
                    _ => 0.0,
                };

                if (danger <= 0)
                {
                    wasCaptured = true;
                    projectedReward -= EstimateCapturePenalty(me, gameState.Tick);
                    break;
                }
            }
            else
            {
                powerMomentum += 0.08;
            }

            if (activeTicks > 0)
            {
                activeTicks--;
                if (activeTicks == 0)
                {
                    activePower = null;
                }
            }

            previousAction = action;
            hasPreviousAction = true;
            action = ChooseGreedyFollowUpAction(
                board,
                geometry,
                x,
                y,
                heldPower,
                activePower,
                activeTicks,
                zookeepers,
                hasPreviousAction ? previousAction : null);
        }

        return new OutlookMetrics(
            ProjectedReward: projectedReward,
            MinDanger: minDanger,
            SafePathRatio: safeSteps / (double)SimulationHorizon,
            TrapPenalty: trapSteps / (double)SimulationHorizon,
            CaptureRisk: captureRisk / SimulationHorizon,
            PowerMomentum: powerMomentum,
            WasCaptured: wasCaptured);
    }

    private static BotAction ChooseGreedyFollowUpAction(
        IReadOnlyDictionary<(int X, int Y), Cell> board,
        BoardGeometry geometry,
        int currentX,
        int currentY,
        PowerUpType? heldPower,
        PowerUpType? activePower,
        int activeTicks,
        IReadOnlyList<ZookeeperPosition> zookeepers,
        BotAction? previousAction)
    {
        var danger = GetNearestZookeeperDistance(currentX, currentY, zookeepers, geometry);
        var bestAction = BotAction.Up;
        var bestScore = double.NegativeInfinity;

        foreach (var action in MovementActions)
        {
            if (!IsLegalMove(board, geometry, currentX, currentY, action))
            {
                continue;
            }

            var (nextX, nextY) = ApplyAction(geometry, currentX, currentY, action);
            var nextCell = board[(nextX, nextY)];
            var nextDanger = GetNearestZookeeperDistance(nextX, nextY, zookeepers, geometry);
            var mobility = CountLegalNeighbors(board, geometry, nextX, nextY);
            var density = GetCollectibleDensity(board.Values, nextX, nextY, 4, geometry);

            var score = NormalizeReward(GetCollectibleReward(nextCell.Content, activePower), 200.0)
                + density * 0.90
                + NormalizeDanger(nextDanger) * (IsProtected(activePower, activeTicks) ? 0.40 : 1.20)
                + mobility / 5.0;

            if (!IsProtected(activePower, activeTicks))
            {
                if (nextDanger <= 0)
                {
                    score -= 10.0;
                }
                else if (nextDanger == 1)
                {
                    score -= 2.5;
                }
                else if (nextDanger == 2)
                {
                    score -= 0.8;
                }
            }

            if (mobility <= 1)
            {
                score -= 0.9;
            }

            if (previousAction.HasValue && IsOpposite(previousAction.Value, action))
            {
                score -= 0.6;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestAction = action;
            }
        }

        if (heldPower.HasValue && ShouldUsePowerNow(heldPower.Value, danger, activePower, board, geometry, currentX, currentY))
        {
            var useItemScore = danger switch
            {
                <= 1 => 3.0,
                2 => 1.4,
                _ => 0.4,
            };

            if (useItemScore >= bestScore - 0.15)
            {
                return BotAction.UseItem;
            }
        }

        return bestScore > double.NegativeInfinity ? bestAction : BotAction.Up;
    }

    private static bool ShouldUsePowerNow(
        PowerUpType heldPower,
        int danger,
        PowerUpType? activePower,
        IReadOnlyDictionary<(int X, int Y), Cell> board,
        BoardGeometry geometry,
        int x,
        int y)
    {
        if (activePower.HasValue)
        {
            return false;
        }

        var localDensity = GetCollectibleDensity(board.Values, x, y, 5, geometry);
        return heldPower switch
        {
            PowerUpType.PowerPellet => danger <= 3,
            PowerUpType.ChameleonCloak => danger <= 3,
            PowerUpType.Scavenger => localDensity >= 0.75,
            PowerUpType.BigMooseJuice => localDensity >= 0.55,
            _ => false,
        };
    }

    private static bool ActivateHeldPower(
        ref PowerUpType? heldPower,
        ref PowerUpType? activePower,
        ref int activeTicks)
    {
        if (!heldPower.HasValue)
        {
            return false;
        }

        activePower = heldPower.Value;
        activeTicks = heldPower.Value switch
        {
            PowerUpType.PowerPellet => PowerPelletDuration,
            PowerUpType.ChameleonCloak => CloakDuration,
            PowerUpType.Scavenger => ScavengerDuration,
            PowerUpType.BigMooseJuice => BigMooseDuration,
            _ => 0,
        };
        heldPower = null;
        return true;
    }

    private static double CollectAtPosition(
        IReadOnlyDictionary<(int X, int Y), Cell> board,
        ISet<(int X, int Y)> consumed,
        int x,
        int y,
        ref PowerUpType? heldPower,
        PowerUpType? activePower)
    {
        if (!board.TryGetValue((x, y), out var cell) || consumed.Contains((x, y)))
        {
            return 0.0;
        }

        var reward = cell.Content switch
        {
            CellContent.Pellet => GetCollectibleReward(cell.Content, activePower),
            CellContent.PowerPellet => GetCollectibleReward(cell.Content, activePower),
            CellContent.ChameleonCloak => 120.0,
            CellContent.Scavenger => 135.0,
            CellContent.BigMooseJuice => 125.0,
            _ => 0.0,
        };

        if (reward <= 0.0)
        {
            return 0.0;
        }

        consumed.Add((x, y));

        heldPower = cell.Content switch
        {
            CellContent.ChameleonCloak => PowerUpType.ChameleonCloak,
            CellContent.Scavenger => PowerUpType.Scavenger,
            CellContent.BigMooseJuice => PowerUpType.BigMooseJuice,
            CellContent.PowerPellet => PowerUpType.PowerPellet,
            _ => heldPower,
        };

        return reward;
    }

    private static IReadOnlyList<ZookeeperPosition> MoveSimulatedZookeepers(
        IReadOnlyDictionary<(int X, int Y), Cell> board,
        BoardGeometry geometry,
        IReadOnlyList<ZookeeperPosition> zookeepers,
        int targetX,
        int targetY,
        bool isProtected)
    {
        if (isProtected)
        {
            return zookeepers;
        }

        var moved = new ZookeeperPosition[zookeepers.Count];
        for (var keeperIndex = 0; keeperIndex < zookeepers.Count; keeperIndex++)
        {
            var keeper = zookeepers[keeperIndex];
            var best = keeper;
            var bestDistance = GetWrappedDistance(geometry, keeper.X, keeper.Y, targetX, targetY);

            foreach (var action in MovementActions)
            {
                var (nextX, nextY) = ApplyAction(geometry, keeper.X, keeper.Y, action);
                if (!board.TryGetValue((nextX, nextY), out var nextCell) || nextCell.Content == CellContent.Wall)
                {
                    continue;
                }

                var distance = GetWrappedDistance(geometry, nextX, nextY, targetX, targetY);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = new ZookeeperPosition(nextX, nextY);
                }
            }

            moved[keeperIndex] = best;
        }

        return moved;
    }

    private static double EstimateCapturePenalty(Animal me, int tick)
    {
        var scoreWeight = Math.Max(EstimatedPelletScore, me.Score * 0.18);
        var streakWeight = Math.Max(0, me.ScoreStreak) * 90.0;
        var lateGameWeight = tick >= 900 ? 220.0 : 0.0;
        return scoreWeight + streakWeight + lateGameWeight;
    }

    private static double GetProtectionWeight(GameState gameState, Animal me)
    {
        var bestOpponentScore = gameState.Animals
            .Where(a => a.Id != me.Id)
            .Select(a => a.Score)
            .DefaultIfEmpty(0)
            .Max();

        var lead = Math.Max(0, me.Score - bestOpponentScore);
        return Math.Clamp(
            0.45
            + me.Score / 32000.0
            + Math.Max(0, me.ScoreStreak) / 5.0
            + gameState.Tick / 1600.0
            + lead / 18000.0,
            0.45,
            3.0);
    }

    private static double NormalizeReward(double value, double scale = 700.0)
    {
        return Math.Clamp(value / scale, -1.0, 1.0);
    }

    private static double NormalizeDanger(int distance)
    {
        return Math.Clamp(distance / 6.0, 0.0, 1.0);
    }

    private static double EvaluateNetwork(IReadOnlyList<double> features)
    {
        var output = OutputBias;

        for (var hiddenIndex = 0; hiddenIndex < HiddenWeights.Length; hiddenIndex++)
        {
            var sum = HiddenBiases[hiddenIndex];
            for (var featureIndex = 0; featureIndex < features.Count; featureIndex++)
            {
                sum += HiddenWeights[hiddenIndex][featureIndex] * features[featureIndex];
            }

            var activation = Math.Max(0.0, sum);
            output += activation * OutputWeights[hiddenIndex];
        }

        return output;
    }

    private static double ScoreUseItemFit(
        Animal me,
        int currentDanger,
        GameState gameState,
        IReadOnlyDictionary<(int X, int Y), Cell> board,
        BoardGeometry geometry)
    {
        if (!me.HeldPowerUp.HasValue)
        {
            return 0.0;
        }

        var nearbyCollectibles = GetCollectibleDensity(board.Values, me.X, me.Y, 5, geometry);
        var nearbyThreats = CountThreatsWithin(me.X, me.Y, gameState.Zookeepers, 3, geometry);

        return me.HeldPowerUp.Value switch
        {
            PowerUpType.PowerPellet => Math.Clamp(0.45 + nearbyCollectibles + (currentDanger <= 3 ? 0.55 : 0.0), 0.0, 1.0),
            PowerUpType.ChameleonCloak => Math.Clamp(0.30 + (currentDanger <= 3 ? 0.70 : 0.0) + nearbyThreats * 0.10, 0.0, 1.0),
            PowerUpType.Scavenger => Math.Clamp(0.20 + nearbyCollectibles * 1.10, 0.0, 1.0),
            PowerUpType.BigMooseJuice => Math.Clamp(0.18 + nearbyCollectibles * 0.85 + (currentDanger <= 2 ? 0.10 : 0.0), 0.0, 1.0),
            _ => 0.0,
        };
    }

    private static double FindBestReachableCollectible(
        IReadOnlyDictionary<(int X, int Y), Cell> board,
        BoardGeometry geometry,
        int startX,
        int startY,
        int maxDepth)
    {
        var visited = new HashSet<(int X, int Y)> { (startX, startY) };
        var queue = new Queue<((int X, int Y) Pos, int Depth)>();
        queue.Enqueue(((startX, startY), 0));
        var best = 0.0;

        while (queue.Count > 0)
        {
            var (pos, depth) = queue.Dequeue();
            if (depth > maxDepth)
            {
                continue;
            }

            if (board.TryGetValue(pos, out var cell))
            {
                var collectibleValue = GetCollectibleReward(cell.Content, null);
                if (collectibleValue > 0)
                {
                    best = Math.Max(best, NormalizeReward(collectibleValue / (depth + 1.0), 250.0));
                }
            }

            if (depth == maxDepth)
            {
                continue;
            }

            foreach (var action in MovementActions)
            {
                var next = ApplyAction(geometry, pos.X, pos.Y, action);
                if (!board.TryGetValue(next, out var nextCell) || nextCell.Content == CellContent.Wall || !visited.Add(next))
                {
                    continue;
                }

                queue.Enqueue((next, depth + 1));
            }
        }

        return best;
    }

    private static double GetCollectibleDensity(IEnumerable<Cell> cells, int centerX, int centerY, int radius, BoardGeometry geometry)
    {
        var score = 0.0;

        foreach (var cell in cells)
        {
            var collectibleValue = GetCollectibleReward(cell.Content, null);
            if (collectibleValue <= 0)
            {
                continue;
            }

            var distance = GetWrappedDistance(geometry, centerX, centerY, cell.X, cell.Y);
            if (distance == 0)
            {
                score += collectibleValue / 800.0;
                continue;
            }

            if (distance <= radius)
            {
                score += collectibleValue / (distance + 1.0) / 900.0;
            }
        }

        return score;
    }

    private static int CountLegalNeighbors(
        IReadOnlyDictionary<(int X, int Y), Cell> board,
        BoardGeometry geometry,
        int centerX,
        int centerY)
    {
        var count = 0;
        foreach (var action in MovementActions)
        {
            var (nextX, nextY) = ApplyAction(geometry, centerX, centerY, action);
            if (board.TryGetValue((nextX, nextY), out var cell) && cell.Content != CellContent.Wall)
            {
                count++;
            }
        }

        return count;
    }

    private static double ScoreReachableSafeArea(
        IReadOnlyDictionary<(int X, int Y), Cell> board,
        BoardGeometry geometry,
        int startX,
        int startY,
        IEnumerable<Zookeeper> zookeepers,
        bool isProtected)
    {
        if (isProtected)
        {
            return 1.0;
        }

        var visited = new HashSet<(int X, int Y)> { (startX, startY) };
        var queue = new Queue<((int X, int Y) Pos, int Depth)>();
        queue.Enqueue(((startX, startY), 0));
        var safeCells = 0;
        const int maxDepth = 5;

        while (queue.Count > 0)
        {
            var (pos, depth) = queue.Dequeue();
            var danger = GetNearestZookeeperDistance(pos.X, pos.Y, zookeepers, geometry);
            var exits = CountLegalNeighbors(board, geometry, pos.X, pos.Y);

            if (danger >= 2 && exits >= 2)
            {
                safeCells++;
            }

            if (depth >= maxDepth)
            {
                continue;
            }

            foreach (var action in MovementActions)
            {
                var next = ApplyAction(geometry, pos.X, pos.Y, action);
                if (!board.TryGetValue(next, out var nextCell) || nextCell.Content == CellContent.Wall || !visited.Add(next))
                {
                    continue;
                }

                queue.Enqueue((next, depth + 1));
            }
        }

        return Math.Clamp(safeCells / 14.0, 0.0, 1.0);
    }

    private static int GetNearestZookeeperDistance(int x, int y, IEnumerable<Zookeeper> zookeepers, BoardGeometry geometry)
    {
        var minDistance = int.MaxValue;
        foreach (var zookeeper in zookeepers)
        {
            minDistance = Math.Min(minDistance, GetWrappedDistance(geometry, x, y, zookeeper.X, zookeeper.Y));
        }

        return minDistance == int.MaxValue ? 12 : minDistance;
    }

    private static int GetNearestZookeeperDistance(int x, int y, IReadOnlyList<ZookeeperPosition> zookeepers, BoardGeometry geometry)
    {
        var minDistance = int.MaxValue;
        foreach (var zookeeper in zookeepers)
        {
            minDistance = Math.Min(minDistance, GetWrappedDistance(geometry, x, y, zookeeper.X, zookeeper.Y));
        }

        return minDistance == int.MaxValue ? 12 : minDistance;
    }

    private static int CountThreatsWithin(int x, int y, IEnumerable<Zookeeper> zookeepers, int distanceThreshold, BoardGeometry geometry)
    {
        var count = 0;
        foreach (var zookeeper in zookeepers)
        {
            if (GetWrappedDistance(geometry, x, y, zookeeper.X, zookeeper.Y) <= distanceThreshold)
            {
                count++;
            }
        }

        return count;
    }

    private static double GetCollectibleReward(CellContent content, PowerUpType? activePower)
    {
        var multiplier = activePower == PowerUpType.BigMooseJuice ? 3 : 1;
        return content switch
        {
            CellContent.Pellet => EstimatedPelletScore * multiplier,
            CellContent.PowerPellet => EstimatedPowerPelletScore * multiplier,
            CellContent.ChameleonCloak => 120,
            CellContent.Scavenger => 135,
            CellContent.BigMooseJuice => 125,
            _ => 0,
        };
    }

    private static bool IsProtected(PowerUpType? activePower, int activeTicks)
    {
        return activeTicks > 0
            && (activePower == PowerUpType.PowerPellet || activePower == PowerUpType.ChameleonCloak);
    }

    private static (int X, int Y) ApplyAction(BoardGeometry geometry, int x, int y, BotAction action)
    {
        var (nextX, nextY) = BotUtils.ApplyMove(x, y, action);
        nextX = WrapCoord(nextX, geometry.Width);
        nextY = WrapCoord(nextY, geometry.Height);
        return (nextX, nextY);
    }

    private static int GetWrappedDistance(BoardGeometry geometry, int x1, int y1, int x2, int y2)
    {
        var dx = Math.Abs(x1 - x2);
        var dy = Math.Abs(y1 - y2);
        dx = Math.Min(dx, geometry.Width - dx);
        dy = Math.Min(dy, geometry.Height - dy);
        return dx + dy;
    }

    private static int WrapCoord(int value, int size)
    {
        var wrapped = value % size;
        return wrapped < 0 ? wrapped + size : wrapped;
    }

    private static bool IsOpposite(BotAction left, BotAction right)
    {
        return (left == BotAction.Up && right == BotAction.Down)
            || (left == BotAction.Down && right == BotAction.Up)
            || (left == BotAction.Left && right == BotAction.Right)
            || (left == BotAction.Right && right == BotAction.Left);
    }

    private static BoardGeometry GetBoardGeometry(IEnumerable<Cell> cells)
    {
        var width = cells.Max(c => c.X) + 1;
        var height = cells.Max(c => c.Y) + 1;
        return new BoardGeometry(width, height);
    }

    private BotAction TryGetExpertAction(GameState gameState, Func<GameState, BotAction> expert, BotAction fallback)
    {
        try
        {
            return expert(gameState);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Expert policy failed. Falling back to {FallbackAction}", fallback);
            return fallback;
        }
    }

    private void ResetMovementMemoryIfCaptured(Animal me)
    {
        if (_lastCapturedCounter != me.CapturedCounter)
        {
            _lastCapturedCounter = me.CapturedCounter;
            _lastPosition = null;
            _previousPosition = null;
            _postCaptureEscapeTicksRemaining = me.CapturedCounter > 0 ? PostCaptureEscapeTicks : 0;
            _recentPositions.Clear();
            return;
        }

        if (_postCaptureEscapeTicksRemaining > 0)
        {
            _postCaptureEscapeTicksRemaining--;
        }
    }

    private bool ShouldUsePostCaptureEscape(Animal me, BoardGeometry geometry, int currentDanger)
    {
        if (_postCaptureEscapeTicksRemaining <= 0)
        {
            return false;
        }

        var spawnDistance = GetWrappedDistance(geometry, me.X, me.Y, me.SpawnX, me.SpawnY);
        return spawnDistance <= PostCaptureSpawnRadius || currentDanger <= EmergencyDangerDistance + 1;
    }

    private void RememberPosition(int x, int y)
    {
        _previousPosition = _lastPosition;
        _lastPosition = (x, y);
        _recentPositions.Enqueue((x, y));
        while (_recentPositions.Count > RecentPositionWindow)
        {
            _recentPositions.Dequeue();
        }
    }

    private int GetRecentVisitCount(int x, int y)
    {
        var count = 0;
        foreach (var position in _recentPositions)
        {
            if (position.X == x && position.Y == y)
            {
                count++;
            }
        }

        return count;
    }

    private void ResetSessionState(bool recreateExperts)
    {
        _lastPosition = null;
        _previousPosition = null;
        _lastCapturedCounter = -1;
        _postCaptureEscapeTicksRemaining = 0;
        _recentPositions.Clear();

        if (recreateExperts)
        {
            _monteCarloBot = new MonteCarloBotService(_logger);
            _clingyBot = new HeuroBotService(_logger);
        }
    }

    private readonly record struct BoardGeometry(int Width, int Height);
    private readonly record struct ZookeeperPosition(int X, int Y);
    private readonly record struct OutlookMetrics(
        double ProjectedReward,
        int MinDanger,
        double SafePathRatio,
        double TrapPenalty,
        double CaptureRisk,
        double PowerMomentum,
        bool WasCaptured);
    private readonly record struct MoveProfile(
        int NextDanger,
        double SafeArea,
        int Mobility,
        double ImmediateReward,
        double ProjectedReward,
        double CaptureRisk,
        double SafePathRatio,
        bool WasCaptured);
}
