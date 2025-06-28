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
using System.Text.Json.Serialization.Metadata;

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
                Console.WriteLine($"Loading process config from: {configPath}");
                
                if (!File.Exists(configPath))
                {
                    Debug.WriteLine($"Process config file not found at: {configPath}");
                    Console.WriteLine($"Process config file not found at: {configPath}");
                    return;
                }

                var json = await File.ReadAllTextAsync(configPath);
                Debug.WriteLine($"Loaded JSON content: {json}");
                Console.WriteLine($"Loaded JSON content length: {json.Length} characters");
                
                // Configure JsonSerializerOptions for trimmed applications
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    TypeInfoResolver = System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.Combine(
                        ProcessConfigJsonContext.Default
                    )
                };
                
                var configs = JsonSerializer.Deserialize<ProcessConfig[]>(json, options);
                Debug.WriteLine($"Deserialized {configs?.Length ?? 0} process configurations");
                Console.WriteLine($"Deserialized {configs?.Length ?? 0} process configurations");

                if (configs != null)
                {
                    foreach (var config in configs)
                    {
                        Debug.WriteLine($"Processing config: {config.Name}");
                        Console.WriteLine($"Processing config: {config.Name}");
                        
                        var workingDir = string.IsNullOrEmpty(config.WorkingDirectory) 
                            ? Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."))
                            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", config.WorkingDirectory));

                        Debug.WriteLine($"Adding process: {config.Name}, WorkingDir: {workingDir}");
                        Console.WriteLine($"Adding process: {config.Name}, WorkingDir: {workingDir}");

                        var processViewModel = new ProcessViewModel { Name = config.Name, Status = "Stopped" };
                        
                        // Add initial helpful information to logs
                        processViewModel.Logs = $"Process: {config.Name}\nCommand: {config.FileName} {config.Arguments}\nWorking Directory: {workingDir}\nStatus: Ready to build/start\n\n--- Build/Run logs will appear below ---\n";

                        // Process environment variables and replace {{GUID}} placeholders
                        var envVars = config.EnvironmentVariables ?? new Dictionary<string, string>();
                        var processedEnvVars = new Dictionary<string, string>();
                        
                        foreach (var kvp in envVars)
                        {
                            var value = kvp.Value;
                            if (value == "{{GUID}}")
                            {
                                value = Guid.NewGuid().ToString();
                                Debug.WriteLine($"Generated GUID for {kvp.Key}: {value}");
                                Console.WriteLine($"Generated GUID for {kvp.Key}: {value}");
                            }
                            processedEnvVars[kvp.Key] = value;
                        }

                        _processes.Add(new ManagedProcess(
                            processViewModel,
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
                            processedEnvVars));
                            
                        Debug.WriteLine($"Successfully added process: {config.Name}");
                        Console.WriteLine($"Successfully added process: {config.Name}");
                    }
                }
                
                Debug.WriteLine($"Total processes loaded: {_processes.Count}");
                Console.WriteLine($"Total processes loaded: {_processes.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load process configuration: {ex}");
                Console.WriteLine($"Failed to load process configuration: {ex}");
                // Don't crash the app - just log the error
            }
        }

        public IEnumerable<ProcessViewModel> GetProcesses() => _processes.Select(p => p.ViewModel);

        public async Task StartAllAsync()
        {
            try
            {
                Debug.WriteLine("StartAllAsync called");
                Console.WriteLine("StartAllAsync called");
                Console.WriteLine($"Total processes available: {_processes.Count}");
                
                foreach (var proc in _processes)
                {
                    Console.WriteLine($"Process: {proc.ViewModel.Name} - Status: {proc.ViewModel.Status}");
                }
                
                var engineProcess = _processes.FirstOrDefault(p => p.ViewModel.Name == "Zooscape Engine");
                if (engineProcess != null && !IsPortAvailable(5000))
                {
                    UpdateProcessStatus(engineProcess.ViewModel, "Port 5000 in use");
                    Console.WriteLine("Port 5000 is in use, aborting");
                    return;
                }

                Console.WriteLine("Starting build process...");
                // Run build process on background thread to avoid UI hanging
                await Task.Run(async () => await BuildBotsAsync());

                Console.WriteLine("Starting processes...");
                // Start processes in parallel to speed up startup
                var startTasks = new List<Task>();
                foreach (var managedProcess in _processes)
                {
                    if (managedProcess.ViewModel.Status.Contains("Failed") || managedProcess.ViewModel.Status.Contains("Error"))
                    {
                        Debug.WriteLine($"Skipping {managedProcess.ViewModel.Name} due to build failure");
                        Console.WriteLine($"Skipping {managedProcess.ViewModel.Name} due to build failure");
                        continue;
                    }

                    Console.WriteLine($"Starting process: {managedProcess.ViewModel.Name}");
                    // Start each process on a separate background task
                    startTasks.Add(Task.Run(async () => await StartProcessSafelyAsync(managedProcess)));
                }
                
                // Wait for all processes to start
                await Task.WhenAll(startTasks);
                
                Console.WriteLine("StartAllAsync completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StartAllAsync failed: {ex}");
                Console.WriteLine($"StartAllAsync failed: {ex}");
                // Don't crash - just log the error
            }
        }

        private async Task StartProcessSafelyAsync(ManagedProcess managedProcess)
        {
            try
            {
                Console.WriteLine($"=== StartProcessSafelyAsync: {managedProcess.ViewModel.Name} ===");
                Console.WriteLine($"Command: {managedProcess.StartInfo.FileName} {managedProcess.StartInfo.Arguments}");
                Console.WriteLine($"Working Directory: {managedProcess.StartInfo.WorkingDirectory}");
                
                UpdateProcessStatus(managedProcess.ViewModel, "Starting...");
                
#if WINDOWS || MACCATALYST || ANDROID
                Console.WriteLine($"Platform: WINDOWS - Starting process...");
                managedProcess.ProcessInstance = Process.Start(managedProcess.StartInfo);
                
                if (managedProcess.ProcessInstance != null)
                {
                    Console.WriteLine($"Process started successfully - PID: {managedProcess.ProcessInstance.Id}");
                    managedProcess.ProcessInstance.EnableRaisingEvents = true;
                    managedProcess.ProcessInstance.Exited += (sender, args) =>
                    {
                        Console.WriteLine($"Process {managedProcess.ViewModel.Name} exited");
                        UpdateProcessStatus(managedProcess.ViewModel, "Stopped");
                    };
                    UpdateProcessStatus(managedProcess.ViewModel, "Running");
                    Console.WriteLine($"Process {managedProcess.ViewModel.Name} status updated to Running");
                }
                else
                {
                    Console.WriteLine($"Process.Start returned null for {managedProcess.ViewModel.Name}");
                    UpdateProcessStatus(managedProcess.ViewModel, "Failed to start");
                }
#else
                Console.WriteLine($"Platform: NOT SUPPORTED - Skipping process start");
                UpdateProcessStatus(managedProcess.ViewModel, "Not supported on this platform");
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start {managedProcess.ViewModel.Name}: {ex}");
                Console.WriteLine($"Exception starting {managedProcess.ViewModel.Name}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
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
                Console.WriteLine("=== BuildBotsAsync started ===");
                var repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
                Debug.WriteLine($"Repository root: {repoRoot}");
                Console.WriteLine($"Repository root: {repoRoot}");

                // Build Zooscape Engine first
                var engineProcess = _processes.FirstOrDefault(p => p.ViewModel.Name == "Zooscape Engine");
                if (engineProcess != null)
                {
                    Console.WriteLine("=== Building Zooscape Engine ===");
                    await RunBuildCommandAsync(
                        "dotnet",
                        "build engine/Zooscape/Zooscape.csproj -c Release",
                        repoRoot,
                        engineProcess.ViewModel);
                }

                // Build all bot projects
                var botBuilds = new[]
                {
                    ("ClingyHeuroBot2", "Bots/ClingyHeuroBot2/ClingyHeuroBot2.csproj"),
                    ("ClingyHeuroBotExp", "Bots/ClingyHeuroBotExp/ClingyHeuroBotExp.csproj"),
                    ("ClingyHeuroBot", "Bots/ClingyHeuroBot/ClingyHeuroBot.csproj"),
                    ("DeepMCTS", "Bots/DeepMCTS/DeepMCTS.csproj"),
                    ("MCTSo4", "Bots/MCTSo4/MCTSo4.csproj"),
                    ("ReferenceBot", "Bots/ReferenceBot/ReferenceBot.csproj")
                };

                foreach (var (botName, projectPath) in botBuilds)
                {
                    var botProcess = _processes.FirstOrDefault(p => p.ViewModel.Name == botName);
                    if (botProcess != null)
                    {
                        Console.WriteLine($"=== Building {botName} ===");
                        await RunBuildCommandAsync(
                            "dotnet",
                            $"build {projectPath} -c Release",
                            repoRoot,
                            botProcess.ViewModel);
                    }
                }

                // Special handling for AdvancedMCTSBot (C++/CMake)
                var mctsBot = _processes.FirstOrDefault(p => p.ViewModel.Name == "AdvancedMCTSBot");
                if (mctsBot != null)
                {
                    Console.WriteLine("=== Building AdvancedMCTSBot (CMake) ===");
                    // Create a custom build script that uses the full CMake path
                    var buildScript = Path.Combine(Path.GetTempPath(), "build_mcts_bot.bat");
                    var cmakePath = @"C:\Program Files\CMake\bin\cmake.exe";
                    var buildDir = Path.Combine(repoRoot, "Bots", "AdvancedMCTSBot", "build");
                    var sourceDir = Path.Combine(repoRoot, "Bots", "AdvancedMCTSBot");
                    
                    var buildScriptContent = $@"@echo off
setlocal

echo Building AdvancedMCTSBot with CMake...
set CMAKE_PATH=""{cmakePath}""
set BUILD_DIR=""{buildDir}""
set SOURCE_DIR=""{sourceDir}""

if not exist %BUILD_DIR% (
    echo Creating build directory: %BUILD_DIR%
    mkdir ""%BUILD_DIR%""
    if errorlevel 1 (
        echo Failed to create build directory.
        exit /b 1
    )
)

pushd ""%BUILD_DIR%""
if errorlevel 1 (
    echo Failed to change to build directory.
    exit /b 1
)

echo Configuring CMake project (source: %SOURCE_DIR%)...
%CMAKE_PATH% ""%SOURCE_DIR%""
if errorlevel 1 (
    echo CMake configuration failed.
    popd
    exit /b 1
)

echo Building project (Release)...
%CMAKE_PATH% --build . --config Release --target AdvancedMCTSBot
set BUILD_ERRORLEVEL=%ERRORLEVEL%
echo CMake build command finished with ERRORLEVEL: %BUILD_ERRORLEVEL%

if %BUILD_ERRORLEVEL% equ 0 (
    echo Build successful.
    popd
    exit /b 0
)

if %BUILD_ERRORLEVEL% neq 0 (
    echo CMake build command returned ERRORLEVEL: %BUILD_ERRORLEVEL%.
    if exist ""%BUILD_DIR%\Release\AdvancedMCTSBot.exe"" (
        echo Target executable AdvancedMCTSBot.exe found. Overriding exit code to 0.
        popd
        exit /b 0
    ) else (
        echo Target executable AdvancedMCTSBot.exe NOT found. Build truly failed.
        popd
        exit /b 1
    )
)
";

                    await File.WriteAllTextAsync(buildScript, buildScriptContent);
                    
                    await RunBuildCommandAsync(
                        "cmd.exe",
                        $"/c \"{buildScript}\"",
                        repoRoot,
                        mctsBot.ViewModel);
                        
                    // Clean up temp script
                    try { File.Delete(buildScript); } catch { }
                }
                
                Console.WriteLine("=== BuildBotsAsync completed ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BuildBotsAsync failed: {ex}");
                Console.WriteLine($"BuildBotsAsync failed: {ex}");
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
                
                Console.WriteLine($"Build process exited with code: {process.ExitCode} for {viewModel.Name}");
                if (process.ExitCode == 0)
                {
                    Debug.WriteLine($"Build succeeded for {viewModel.Name}");
                    Console.WriteLine($"✅ Build succeeded for {viewModel.Name}");
                    UpdateProcessStatus(viewModel, "Build Succeeded");
                }
                else
                {
                    Debug.WriteLine($"Build failed for {viewModel.Name} with exit code {process.ExitCode}");
                    Console.WriteLine($"❌ Build failed for {viewModel.Name} with exit code {process.ExitCode}");
                    Console.WriteLine($"Build output for {viewModel.Name}:");
                    Console.WriteLine($"STDOUT: {outputBuilder}");
                    Console.WriteLine($"STDERR: {errorBuilder}");
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
