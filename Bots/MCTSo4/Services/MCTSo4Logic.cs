using System;
using System.Threading.Tasks;
using MCTSo4.Algorithms.MCTS;
using MCTSo4.Enums;
using MCTSo4.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace MCTSo4.Services
{
    public class MCTSo4Logic
    {
        private readonly HubConnection _connection;

        public MCTSo4Logic(HubConnection connection)
        {
            _connection = connection;
        }

        public async Task StartAsync(string token, string nickName)
        {
            Console.WriteLine($"Starting MCTSo4Logic for {nickName}");
            try
            {
                BotCommand botCommand = null!;
                _connection.On<Guid>(
                    "Registered",
                    id =>
                    {
                        try
                        {
                            Console.WriteLine($"Registered: {id}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error in Registered handler: {ex}");
                        }
                    }
                );
                _connection.On<MCTSGameState>(
                    "GameState",
                    state =>
                    {
                        try
                        {
                            // Received state directly, no manual parsing
                            var meta = AdaptiveStrategyController.DetermineCurrentMetaStrategy(
                                state
                            );
                            var parameters = AdaptiveStrategyController.ConfigureParameters(meta);
                            var move = MctsController.MCTS_GetBestAction(state, parameters);
                            botCommand = new BotCommand { Action = move };
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error in GameState handler: {ex}");
                        }
                    }
                );
                _connection.On<string>(
                    "Disconnect",
                    async reason =>
                    {
                        try
                        {
                            Console.WriteLine($"Disconnected: {reason}");
                            await _connection.StopAsync();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error in Disconnect handler: {ex}");
                        }
                    }
                );
                _connection.Closed += async error =>
                {
                    Console.WriteLine($"Connection closed: {error}");
                    await Task.CompletedTask;
                };

                // Connection should be started in Program.cs, not here.
                Console.WriteLine("Assuming connection already started (handled in Program.cs)");

                try
                {
                    await _connection.InvokeAsync("Register", token, nickName);
                    Console.WriteLine("Sent Register message");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during registration: {ex}");
                    return;
                }

                try
                {
                    while (
                        _connection.State == HubConnectionState.Connected
                        || _connection.State == HubConnectionState.Connecting
                    )
                    {
                        if (botCommand != null)
                        {
                            try
                            {
                                await _connection.SendAsync("BotCommand", botCommand);
                                botCommand = null!;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error sending BotCommand: {ex}");
                            }
                        }
                        await Task.Delay(10); // Prevent tight loop
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in main loop: {ex}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error in StartAsync: {ex}");
            }
        }
    }
}
