using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using HeuroBot.Enums;
using HeuroBot.Models;

namespace HeuroBot.Services;

public class HeuroBotService
{
    private Guid _botId;
    private readonly string _pythonExecutable = "python";

    public void SetBotId(Guid botId) => _botId = botId;

    public BotCommand ProcessState(GameState state)
    {
        try
        {
            // Serialize game state to JSON
            var gameStateJson = JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            // Create temporary file for game state
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, gameStateJson);

            try
            {
                // Call Python script
                var pythonScript = Path.Combine(Directory.GetCurrentDirectory(), "python_caller.py");
                var startInfo = new ProcessStartInfo
                {
                    FileName = _pythonExecutable,
                    Arguments = $"\"{pythonScript}\" \"{tempFile}\" \"{_botId}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory()
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start Python process");
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Python script failed with exit code {process.ExitCode}: {error}");
                }

                // Parse action from output
                var actionString = output.Trim();
                if (Enum.TryParse<BotAction>(actionString, true, out var action))
                {
                    Console.WriteLine($"RL Bot chose action: {action}");
                    return new BotCommand { Action = action };
                }
                else
                {
                    throw new InvalidOperationException($"Failed to parse action from Python output: {output}");
                }
            }
            finally
            {
                // Clean up temp file
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling Python RL agent: {ex.Message}");
            throw; // Re-throw the exception since we expect models to always be available
        }
    }
}
