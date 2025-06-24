#nullable disable
using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ZooscapeRunner.Models;
using ZooscapeRunner.ViewModels;
using Microsoft.UI.Dispatching;

#if WINDOWS
using Windows.Storage;
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
            var repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));

            try
            {
                string json = "";
                
#if WINDOWS && !HAS_UNO
                try
                {
                    var installFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
                    var configFile = await installFolder.GetFileAsync("Assets\\processes.json");
                    json = await Windows.Storage.FileIO.ReadTextAsync(configFile);
                }
                catch
                {
                    // Fallback to regular file access
                    var assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets", "processes.json");
                    if (File.Exists(assetsPath))
                    {
                        json = await File.ReadAllTextAsync(assetsPath);
                    }
                }
#else
                // For non-Windows platforms or Uno platforms, try to read from Assets folder in the app directory
                var assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets", "processes.json");
                if (!File.Exists(assetsPath))
                {
                    // Fallback to current directory
                    assetsPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "processes.json");
                }
                
                if (File.Exists(assetsPath))
                {
                    json = await File.ReadAllTextAsync(assetsPath);
                }
                else
                {
                    Debug.WriteLine($"Config file not found at: {assetsPath}");
                    return;
                }
#endif

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var configs = JsonSerializer.Deserialize<List<ProcessConfig>>(json, options);

                if (configs != null)
                {
                    foreach (var config in configs)
                    {
                        var workingDir = string.IsNullOrEmpty(config.WorkingDirectory)
                            ? repoRoot
                            : Path.Combine(repoRoot, config.WorkingDirectory);

                        _processes.Add(new ManagedProcess(
                            config.Name,
                            config.FileName,
                            config.Arguments,
                            workingDir,
                            config.EnvironmentVariables));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load process configuration: {ex}");
                // Optionally, handle this error more gracefully in the UI
            }
        }

        public IEnumerable<ProcessViewModel> GetProcesses() => _processes.Select(p => p.ViewModel);

        public async Task StartAllAsync()
        {
            var engineProcess = _processes.FirstOrDefault(p => p.ViewModel.Name == "Zooscape Engine");
            if (engineProcess != null && !IsPortAvailable(5000))
            {
                engineProcess.ViewModel.Status = "Port 5000 in use";
                return; // Stop if port is not available
            }

            await BuildBotsAsync();

            foreach (var managedProcess in _processes)
            {
                if (managedProcess.ViewModel.Status.Contains("Failed") || managedProcess.ViewModel.Status.Contains("Error"))
                {
                    continue;
                }

                try
                {
                    managedProcess.ViewModel.Status = "Starting...";
#if WINDOWS || MACCATALYST || ANDROID
                    managedProcess.ProcessInstance = Process.Start(managedProcess.StartInfo);
                    if (managedProcess.ProcessInstance != null)
                    {
                        managedProcess.ProcessInstance.EnableRaisingEvents = true;
                        managedProcess.ProcessInstance.Exited += (sender, args) =>
                        {
                            managedProcess.ViewModel.Status = "Stopped";
                        };
                        managedProcess.ViewModel.Status = "Running";
                    }
                    else
                    {
                        managedProcess.ViewModel.Status = "Failed to start";
                    }
#else
                    managedProcess.ViewModel.Status = "Not supported on this platform";
#endif
                }
                catch (Exception ex)
                {
                    managedProcess.ViewModel.Status = $"Error: {ex.Message}";
                }
            }
        }

        private async Task BuildBotsAsync()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));

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

        private async Task RunBuildCommandAsync(string fileName, string arguments, string workingDirectory, ProcessViewModel viewModel)
        {
            viewModel.Status = "Building...";
            try
            {
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
                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    viewModel.Status = "Build Succeeded";
                }
                else
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    Debug.WriteLine($"Build failed for {viewModel.Name}: {error}");
                    viewModel.Status = $"Build Failed";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Build exception for {viewModel.Name}: {ex}");
                viewModel.Status = $"Build Error";
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
            catch
            {
                // If port checking fails, assume it's available
                return true;
            }
#else
            // On platforms where IPGlobalProperties is not available, assume port is available
            return true;
#endif
        }

        public Task StopAllAsync()
        {
            foreach (var managedProcess in _processes)
            {
                try
                {
#if WINDOWS || MACCATALYST || ANDROID
                    if (managedProcess.ProcessInstance != null && !managedProcess.ProcessInstance.HasExited)
                    {
                        managedProcess.ProcessInstance.Kill(true);
                        managedProcess.ViewModel.Status = "Stopped";
                    }
#else
                    managedProcess.ViewModel.Status = "Stop not supported on this platform";
#endif
                }
                catch (Exception ex)
                {
                    managedProcess.ViewModel.Status = $"Error: {ex.Message}";
                }
            }
            return Task.CompletedTask;
        }

        public void StartAutoRestart()
        {
            _autoRestartTimer = new Timer(OnTimerTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        private async void OnTimerTick(object? state)
        {
            _remainingSeconds--;

            var timeSpan = TimeSpan.FromSeconds(_remainingSeconds);
            var message = $"Auto-restart in: {timeSpan:mm\\:ss}";
            
            // Ensure UI updates happen on the main thread
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
                await Task.Delay(1000); // Give processes time to shut down
                await StartAllAsync();
                _remainingSeconds = 180; // Reset timer
            }
        }
    }
}
