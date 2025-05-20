using System;
using System.Diagnostics;
using System.Threading.Tasks;
using MCTSo4.Algorithms.MCTS;
using MCTSo4.Enums;
using MCTSo4.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Serilog;

namespace MCTSo4.Services
{
    public class MCTSo4Logic
    {
        private readonly HubConnection _connection;
        private readonly ILogger _log;
        private Guid _botId;

        // Self-tuning time budget parameters
        private int _consecutiveOverruns = 0;
        private int _consecutiveUnderruns = 0;
        private int _timeBudgetAdjustment = 0; // Positive means we've reduced the budget, negative means we've increased it
        private const int MaxAdjustment = 70; // Maximum amount to adjust the time budget by (in ms)

        public MCTSo4Logic(HubConnection connection)
        {
            _connection = connection;
            _log = Log.ForContext<MCTSo4Logic>();
        }

        public async Task StartAsync(string token, string nickName)
        {
            _log.Information("Starting MCTSo4Logic for {NickName}", nickName);
            try
            {
                _connection.On<Guid>(
                    "Registered",
                    id =>
                    {
                        try
                        {
                            _botId = id;
                            _log.Information("Registered with ID: {BotId}", _botId);
                        }
                        catch (Exception ex)
                        {
                            _log.Error(ex, "Error in Registered handler");
                        }
                    }
                );
                _connection.On<MCTSGameState>(
                    "GameState",
                    async state =>
                    {
                        try
                        {
                            if (_botId == Guid.Empty)
                            {
                                _log.Warning(
                                    "GameState received for Tick {Tick} but BotId is not yet set (still Guid.Empty). Skipping MCTS for this tick.",
                                    state?.Tick
                                );
                                return;
                            }

                            _log.Debug("Received GameState for Tick {Tick}", state?.Tick);
                            if (state == null)
                            {
                                _log.Warning("Received null GameState.");
                                return;
                            }
                            var meta = AdaptiveStrategyController.DetermineCurrentMetaStrategy(
                                state
                            );
                            _log.Debug("Determined MetaStrategy: {MetaStrategy}", meta);
                            var parameters = AdaptiveStrategyController.ConfigureParameters(meta);

                            // Apply self-tuning adjustment to the time budget
                            parameters.MaxTimePerMoveMs = Math.Max(
                                50,
                                parameters.MaxTimePerMoveMs - _timeBudgetAdjustment
                            );

                            _log.Debug(
                                "Using time budget of {TimeBudget}ms for MCTS calculation (adjustment: {Adjustment}ms)",
                                parameters.MaxTimePerMoveMs,
                                _timeBudgetAdjustment
                            );

                            _log.Information(
                                "Calculating MCTS best action for Tick {Tick}...",
                                state.Tick
                            );
                            var stopwatch = Stopwatch.StartNew();
                            var move = MctsController.MCTS_GetBestAction(
                                state,
                                _botId,
                                parameters,
                                stopwatch
                            );
                            stopwatch.Stop();
                            _log.Information(
                                "Calculated MCTS best action: {Move} for Tick {Tick}. Duration: {ElapsedMilliseconds}ms",
                                move,
                                state.Tick,
                                stopwatch.ElapsedMilliseconds
                            );

                            // Self-tuning logic
                            if (stopwatch.ElapsedMilliseconds > parameters.MaxTimePerMoveMs + 10)
                            {
                                _log.Warning(
                                    "MCTS_GetBestAction for Tick {Tick} took {ElapsedMilliseconds}ms, exceeding {TimeBudget}ms budget!",
                                    state.Tick,
                                    stopwatch.ElapsedMilliseconds,
                                    parameters.MaxTimePerMoveMs
                                );

                                // Increment consecutive overruns and adjust time budget
                                _consecutiveOverruns++;
                                _consecutiveUnderruns = 0;
                                if (_consecutiveOverruns >= 3)
                                {
                                    // After 3 consecutive overruns, reduce the time budget
                                    int newAdjustment = Math.Min(
                                        MaxAdjustment,
                                        _timeBudgetAdjustment + 5 + (_consecutiveOverruns - 3) * 2
                                    );

                                    if (newAdjustment != _timeBudgetAdjustment)
                                    {
                                        _log.Information(
                                            "Auto-tuning: Increasing time budget adjustment from {OldAdjustment}ms to {NewAdjustment}ms after {Overruns} consecutive overruns",
                                            _timeBudgetAdjustment,
                                            newAdjustment,
                                            _consecutiveOverruns
                                        );
                                        _timeBudgetAdjustment = newAdjustment;
                                    }
                                }
                            }
                            else if (
                                stopwatch.ElapsedMilliseconds
                                < parameters.MaxTimePerMoveMs * 0.8
                            )
                            {
                                // If we're using less than 80% of the budget, we might be able to use more time
                                _consecutiveUnderruns++;
                                _consecutiveOverruns = 0;

                                if (_consecutiveUnderruns >= 5 && _timeBudgetAdjustment > 0)
                                {
                                    // After 5 consecutive underruns, increase the time budget (reduce adjustment)
                                    int newAdjustment = Math.Max(
                                        0,
                                        _timeBudgetAdjustment - 2 - (_consecutiveUnderruns - 5)
                                    );

                                    if (newAdjustment != _timeBudgetAdjustment)
                                    {
                                        _log.Information(
                                            "Auto-tuning: Decreasing time budget adjustment from {OldAdjustment}ms to {NewAdjustment}ms after {Underruns} consecutive underruns",
                                            _timeBudgetAdjustment,
                                            newAdjustment,
                                            _consecutiveUnderruns
                                        );
                                        _timeBudgetAdjustment = newAdjustment;
                                    }
                                }
                            }
                            else
                            {
                                // Reset counters if we're within a good range
                                _consecutiveOverruns = Math.Max(0, _consecutiveOverruns - 1);
                                _consecutiveUnderruns = Math.Max(0, _consecutiveUnderruns - 1);
                            }

                            // Create the bot command and send it immediately
                            var botCommand = new BotCommand { Action = move };
                            try
                            {
                                _log.Debug(
                                    "Attempting to send BotCommand: {@BotCommand}",
                                    botCommand
                                );
                                await _connection.SendAsync("BotCommand", botCommand);
                                _log.Information("Sent BotCommand: {Action}", botCommand.Action);
                            }
                            catch (Exception ex)
                            {
                                _log.Error(ex, "Error sending BotCommand");
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.Error(ex, "Error in GameState handler");
                        }
                    }
                );
                _connection.On<string>(
                    "Disconnect",
                    async reason =>
                    {
                        try
                        {
                            _log.Information("Disconnected by server. Reason: {Reason}", reason);
                            await _connection.StopAsync();
                        }
                        catch (Exception ex)
                        {
                            _log.Error(ex, "Error in Disconnect handler");
                        }
                    }
                );
                _connection.Closed += async error =>
                {
                    if (error != null)
                    {
                        _log.Error(
                            error,
                            "Connection closed with error: {ErrorMessage}",
                            error.Message
                        );
                    }
                    else
                    {
                        _log.Information("Connection closed without error.");
                    }
                    await Task.CompletedTask;
                };

                _log.Information("Assuming connection already started (handled in Program.cs)");

                try
                {
                    await _connection.InvokeAsync("Register", token, nickName);
                    _log.Information(
                        "Sent Register message for {NickName} with token {Token}",
                        nickName,
                        token
                    );
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error during registration for {NickName}", nickName);
                    return;
                }

                // No longer using a while loop - commands are sent directly in the GameState handler
                _log.Information(
                    "Bot ready to receive game states for {NickName}. Connection state: {ConnectionState}",
                    nickName,
                    _connection.State
                );
            }
            catch (Exception ex)
            {
                _log.Fatal(ex, "Fatal error in StartAsync for {NickName}", nickName);
            }
        }
    }
}
