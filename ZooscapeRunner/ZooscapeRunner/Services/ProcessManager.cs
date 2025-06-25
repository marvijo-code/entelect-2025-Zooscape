#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ZooscapeRunner.Models;
using ZooscapeRunner.ViewModels;
using Microsoft.UI.Dispatching;
using System.Text;

#if WINDOWS
// using System.Net.NetworkInformation; // Already included above
#endif

namespace ZooscapeRunner.Services
{
    public class ProcessManager : IProcessManager
    {
        private readonly List<ManagedProcess> _processes = new();
        private Timer? _autoRestartTimer;
        private int _remainingSeconds = 180;
        private readonly DispatcherQueue _dispatcher;

        public event Action<string>? RestartTimerTick;

        private ProcessManager(DispatcherQueue dispatcher) 
        { 
            _dispatcher = dispatcher;
        }

        public static async Task<ProcessManager> CreateAsync()
        {
            var dispatcher = DispatcherQueue.GetForCurrentThread();
            var manager = new ProcessManager(dispatcher);
            await manager.LoadProcessesConfigAsync();
            return manager;
        }

        private async Task LoadProcessesConfigAsync()
        {
            try
            {
                var configPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "processes.json");
                Debug.WriteLine($"Loading process config from: {configPath}");
                
                if (!File.Exists(configPath))
                {
                    Debug.WriteLine($"Process config file not found at: {configPath}");
                    return;
                }

                var json = await File.ReadAllTextAsync(configPath);
                var configs = JsonSerializer.Deserialize<ProcessConfig[]>(json);

                if (configs != null)
                {
                    foreach (var config in configs)
                    {
                        var workingDir = string.IsNullOrEmpty(config.WorkingDirectory) 
                            ? Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."))
                            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", config.WorkingDirectory));

                        Debug.WriteLine($"Adding process: {config.Name}, WorkingDir: {workingDir}");

                        _processes.Add(new ManagedProcess(
                            new ProcessViewModel { Name = config.Name, Status = "Stopped" },
                            new ProcessStartInfo
                            {
                                FileName = config.FileName,
                                Arguments = config.Arguments,
                                WorkingDirectory = workingDir,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            },
                            workingDir,
                            config.EnvironmentVariables));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load process configuration: {ex}");
                // Don't crash the app - just log the error
            }
        }

        public IEnumerable<ProcessViewModel> GetProcesses() => _processes.Select(p => p.ViewModel);

        public async Task StartAllAsync()
        {
            try
            {
                Debug.WriteLine("StartAllAsync called");
                
                var engineProcess = _processes.FirstOrDefault(p => p.ViewModel.Name == "Zooscape Engine");
                if (engineProcess != null && !IsPortAvailable(5000))
                {
                    UpdateProcessStatus(engineProcess.ViewModel, "Port 5000 in use");
                    return;
                }

                await BuildBotsAsync();

                foreach (var managedProcess in _processes)
                {
                    if (managedProcess.ViewModel.Status.Contains("Failed") || managedProcess.ViewModel.Status.Contains("Error"))
                    {
                        Debug.WriteLine($"Skipping {managedProcess.ViewModel.Name} due to build failure");
                        continue;
                    }

                    await StartProcessSafelyAsync(managedProcess);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StartAllAsync failed: {ex}");
                // Don't crash - just log the error
            }
        }

        private async Task StartProcessSafelyAsync(ManagedProcess managedProcess)
        {
            try
            {
                UpdateProcessStatus(managedProcess.ViewModel, "Starting...");
                
#if WINDOWS || MACCATALYST || ANDROID
                managedProcess.ProcessInstance = Process.Start(managedProcess.StartInfo);
                if (managedProcess.ProcessInstance != null)
                {
                    managedProcess.ProcessInstance.EnableRaisingEvents = true;
                    managedProcess.ProcessInstance.Exited += (sender, args) =>
                    {
                        UpdateProcessStatus(managedProcess.ViewModel, "Stopped");
                    };
                    UpdateProcessStatus(managedProcess.ViewModel, "Running");
                }
                else
                {
                    UpdateProcessStatus(managedProcess.ViewModel, "Failed to start");
                }
#else
                UpdateProcessStatus(managedProcess.ViewModel, "Not supported on this platform");
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start {managedProcess.ViewModel.Name}: {ex}");
                UpdateProcessStatus(managedProcess.ViewModel, $"Error: {ex.Message}");
            }
        }

        private void UpdateProcessStatus(ProcessViewModel viewModel, string status)
        {
            if (_dispatcher != null)
            {
                _dispatcher.TryEnqueue(() =>
                {
                    viewModel.Status = status;
                    Debug.WriteLine($"{viewModel.Name}: {status}");
                });
            }
            else
            {
                viewModel.Status = status;
                Debug.WriteLine($"{viewModel.Name}: {status}");
            }
        }

        private async Task BuildBotsAsync()
        {
            try
            {
                Debug.WriteLine("BuildBotsAsync started");
                var repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
                Debug.WriteLine($"Repository root: {repoRoot}");

                var clingyBot = _processes.FirstOrDefault(p => p.ViewModel.Name == "ClingyHeuroBot2");
                if (clingyBot != null)
                {
                    await RunBuildCommandAsync(
                        "dotnet",
                        "build Bots/ClingyHeuroBot2/ClingyHeuroBot2.csproj -c Release",
                        repoRoot,
                        clingyBot.ViewModel);
                }

                var mctsBot = _processes.FirstOrDefault(p => p.ViewModel.Name == "AdvancedMCTSBot");
                if (mctsBot != null)
                {
                    await RunBuildCommandAsync(
                        "cmd.exe",
                        "/c build.bat",
                        Path.Combine(repoRoot, "Bots", "AdvancedMCTSBot"),
                        mctsBot.ViewModel);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BuildBotsAsync failed: {ex}");
                // Don't crash - just log the error
            }
        }

        private async Task RunBuildCommandAsync(string fileName, string arguments, string workingDirectory, ProcessViewModel viewModel)
        {
            try
            {
                UpdateProcessStatus(viewModel, "Building...");
                Debug.WriteLine($"Building {viewModel.Name}: {fileName} {arguments} in {workingDirectory}");

                if (!Directory.Exists(workingDirectory))
                {
                    var errorMsg = $"Working directory does not exist: {workingDirectory}";
                    Debug.WriteLine(errorMsg);
                    UpdateProcessStatus(viewModel, "Build Failed - Directory not found");
                    viewModel.Logs = errorMsg;
                    return;
                }

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        WorkingDirectory = workingDirectory,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                // Real-time log updates
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                        Debug.WriteLine($"[{viewModel.Name} OUTPUT] {e.Data}");
                        
                        // Update logs in real-time on UI thread
                        if (_dispatcher != null)
                        {
                            _dispatcher.TryEnqueue(() =>
                            {
                                viewModel.Logs = $"{outputBuilder}\n{errorBuilder}".Trim();
                            });
                        }
                        else
                        {
                            viewModel.Logs = $"{outputBuilder}\n{errorBuilder}".Trim();
                        }
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                        Debug.WriteLine($"[{viewModel.Name} ERROR] {e.Data}");
                        
                        // Update logs in real-time on UI thread
                        if (_dispatcher != null)
                        {
                            _dispatcher.TryEnqueue(() =>
                            {
                                viewModel.Logs = $"{outputBuilder}\n{errorBuilder}".Trim();
                            });
                        }
                        else
                        {
                            viewModel.Logs = $"{outputBuilder}\n{errorBuilder}".Trim();
                        }
                    }
                };

                Debug.WriteLine($"Starting build process: {fileName} {arguments}");
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Add timeout to prevent hanging
                var timeout = TimeSpan.FromMinutes(5);
                if (!process.WaitForExit((int)timeout.TotalMilliseconds))
                {
                    process.Kill();
                    var timeoutMsg = $"Build timed out after {timeout.TotalMinutes} minutes";
                    Debug.WriteLine(timeoutMsg);
                    UpdateProcessStatus(viewModel, "Build Failed - Timeout");
                    viewModel.Logs = $"{outputBuilder}\n{errorBuilder}\n{timeoutMsg}";
                    return;
                }

                // Wait a bit more for output to be processed
                await Task.Delay(500);

                var allOutput = $"{outputBuilder}\n{errorBuilder}".Trim();
                viewModel.Logs = allOutput;

                Debug.WriteLine($"Build process exited with code: {process.ExitCode}");
                Debug.WriteLine($"Build output length: {allOutput.Length} characters");
                
                if (process.ExitCode == 0)
                {
                    Debug.WriteLine($"Build succeeded for {viewModel.Name}");
                    UpdateProcessStatus(viewModel, "Build Succeeded");
                }
                else
                {
                    Debug.WriteLine($"Build failed for {viewModel.Name} with exit code {process.ExitCode}");
                    UpdateProcessStatus(viewModel, $"Build Failed (Exit Code: {process.ExitCode})");
                    
                    // If no output was captured, add a helpful message
                    if (string.IsNullOrEmpty(allOutput))
                    {
                        viewModel.Logs = $"Build failed with exit code {process.ExitCode}\nNo output captured from build process.\nCommand: {fileName} {arguments}\nWorking Directory: {workingDirectory}";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Build exception for {viewModel.Name}: {ex}");
                UpdateProcessStatus(viewModel, "Build Error");
                viewModel.Logs = $"Build exception: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}\n\nCommand: {fileName} {arguments}\nWorking Directory: {workingDirectory}";
            }
        }

        private bool IsPortAvailable(int port)
        {
#if WINDOWS || MACCATALYST
            try
            {
                var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
                var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();

                foreach (var endpoint in tcpConnInfoArray)
                {
                    if (endpoint.Port == port)
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Port check failed: {ex.Message}");
                return true;
            }
#else
            return true;
#endif
        }

        public Task StopAllAsync()
        {
            try
            {
                foreach (var managedProcess in _processes)
                {
                    try
                    {
#if WINDOWS || MACCATALYST || ANDROID
                        if (managedProcess.ProcessInstance != null && !managedProcess.ProcessInstance.HasExited)
                        {
                            managedProcess.ProcessInstance.Kill(true);
                            UpdateProcessStatus(managedProcess.ViewModel, "Stopped");
                        }
#else
                        UpdateProcessStatus(managedProcess.ViewModel, "Stop not supported on this platform");
#endif
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to stop {managedProcess.ViewModel.Name}: {ex}");
                        UpdateProcessStatus(managedProcess.ViewModel, $"Stop Error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StopAllAsync failed: {ex}");
            }
            return Task.CompletedTask;
        }

        public void StartAutoRestart()
        {
            try
            {
                _autoRestartTimer = new Timer(OnTimerTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start auto-restart timer: {ex}");
            }
        }

        private async void OnTimerTick(object? state)
        {
            try
            {
                _remainingSeconds--;

                var timeSpan = TimeSpan.FromSeconds(_remainingSeconds);
                var message = $"Auto-restart in: {timeSpan:mm\\:ss}";
                
                if (_dispatcher != null)
                {
                    _dispatcher.TryEnqueue(() =>
                    {
                        RestartTimerTick?.Invoke(message);
                    });
                }
                else
                {
                    RestartTimerTick?.Invoke(message);
                }

                if (_remainingSeconds <= 0)
                {
                    await StopAllAsync();
                    await Task.Delay(1000);
                    await StartAllAsync();
                    _remainingSeconds = 180;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Timer tick failed: {ex}");
            }
        }
    }
}
