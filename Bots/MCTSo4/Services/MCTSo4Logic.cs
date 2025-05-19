using System;
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
                            _log.Information("Registered with ID: {BotId}", id);
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
                            _log.Debug("Received GameState: {@GameState}", state);
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
                            var move = MctsController.MCTS_GetBestAction(state, parameters);
                            _log.Information("Calculated move: {Move}", move);
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
