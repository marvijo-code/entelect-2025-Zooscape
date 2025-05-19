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
                BotCommand botCommand = null!;
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
                    state =>
                    {
                        try
                        {
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
                            _log.Debug("Configured MCTS Parameters: {@MCTSParameters}", parameters);

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

                            if (stopwatch.ElapsedMilliseconds > 150)
                            {
                                _log.Warning(
                                    "MCTS_GetBestAction for Tick {Tick} took {ElapsedMilliseconds}ms, exceeding 150ms budget!",
                                    state.Tick,
                                    stopwatch.ElapsedMilliseconds
                                );
                            }

                            botCommand = new BotCommand { Action = move };
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

                try
                {
                    _log.Information(
                        "Entering main bot loop for {NickName}. Connection state: {ConnectionState}",
                        nickName,
                        _connection.State
                    );
                    while (_connection.State == HubConnectionState.Connected)
                    {
                        if (botCommand != null)
                        {
                            try
                            {
                                _log.Debug(
                                    "Attempting to send BotCommand: {@BotCommand}",
                                    botCommand
                                );
                                await _connection.SendAsync("BotCommand", botCommand);
                                _log.Information("Sent BotCommand: {Action}", botCommand.Action);
                                botCommand = null!;
                            }
                            catch (Exception ex)
                            {
                                _log.Error(ex, "Error sending BotCommand");
                            }
                        }
                        await Task.Delay(10);
                    }
                    _log.Warning(
                        "Exited main bot loop for {NickName}. Connection state: {ConnectionState}",
                        nickName,
                        _connection.State
                    );
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error in main bot loop for {NickName}", nickName);
                }
            }
            catch (Exception ex)
            {
                _log.Fatal(ex, "Fatal error in StartAsync for {NickName}", nickName);
            }
        }
    }
}
