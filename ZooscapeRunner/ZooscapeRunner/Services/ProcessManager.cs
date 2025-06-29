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

                // Cache the configs for later use
                _processConfigs = configs;

                if (configs != null)
                {
                    foreach (var config in configs)
                    {
                        Debug.WriteLine($"Processing config: {config.Name}");
                        Console.WriteLine($"Processing config: {config.Name}");
                        
                        // Fix working directory calculation - find the actual project root
                        // Navigate from the application directory to the solution root
                        var appDir = AppContext.BaseDirectory; // e.g., C:\dev\2025-Zooscape\ZooscapeRunner\ZooscapeRunner\bin\Debug\net8.0-windows10.0.19041.0\
                        
                        // Go up to find the solution root (look for directory containing engine/, Bots/, etc.)
                        var currentDir = new DirectoryInfo(appDir);
                        string projectRoot = null;
                        
                        // Go up until we find a directory that contains "engine" and "Bots" folders
                        while (currentDir != null && projectRoot == null)
                        {
                            if (Directory.Exists(Path.Combine(currentDir.FullName, "engine")) && 
                                Directory.Exists(Path.Combine(currentDir.FullName, "Bots")))
                            {
                                projectRoot = currentDir.FullName;
                                break;
                            }
                            currentDir = currentDir.Parent;
                        }
                        
                        // Fallback if we can't find it
                        if (projectRoot == null)
                        {
                            projectRoot = Path.GetFullPath(Path.Combine(appDir, "..", "..", "..", "..", ".."));
                        }
                        
                        var workingDir = string.IsNullOrEmpty(config.WorkingDirectory) 
                            ? projectRoot 
                            : Path.GetFullPath(Path.Combine(projectRoot, config.WorkingDirectory));

                        Debug.WriteLine($"App directory: {appDir}");
                        Console.WriteLine($"App directory: {appDir}");
                        Debug.WriteLine($"Found project root: {projectRoot}");
                        Console.WriteLine($"Found project root: {projectRoot}");
                        Debug.WriteLine($"Adding process: {config.Name}, WorkingDir: {workingDir}");
                        Console.WriteLine($"Adding process: {config.Name}, WorkingDir: {workingDir}");

                        var processViewModel = new ProcessViewModel 
                        { 
                            Name = config.Name, 
                            Status = "Stopped",
                            ProcessType = config.ProcessType ?? "Bot"
                        };
                        
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
                Console.WriteLine("=== StartAllAsync called ===");
                Console.WriteLine($"Total processes available: {_processes.Count}");
                
                foreach (var proc in _processes)
                {
                    Console.WriteLine($"Process: {proc.ViewModel.Name} - Status: {proc.ViewModel.Status}");
                }
                
                // Check if port 5000 is available for the engine
                var engineProcess = _processes.FirstOrDefault(p => p.ViewModel.Name == "Zooscape Engine");
                if (engineProcess != null && !IsPortAvailable(5000))
                {
                    Console.WriteLine("⚠️ Port 5000 is in use - attempting to stop existing process");
                    await StopProcessOnPortAsync(5000, "Zooscape Engine");
                    
                    // Recheck after stopping
                    if (!IsPortAvailable(5000))
                    {
                        UpdateProcessStatus(engineProcess.ViewModel, "Port 5000 still in use");
                        Console.WriteLine("❌ Port 5000 is still in use after cleanup attempt, aborting start");
                        return;
                    }
                }

                Console.WriteLine("=== Starting build process ===");
                // Run build process and wait for completion
                await BuildBotsAsync();

                Console.WriteLine("=== Build process completed - Starting processes ===");
                
                // Start Engine first (it's a dependency for bots)
                if (engineProcess != null && !engineProcess.ViewModel.Status.Contains("Failed") && !engineProcess.ViewModel.Status.Contains("Error"))
                {
                    Console.WriteLine("Starting Zooscape Engine first...");
                    await StartProcessSafelyAsync(engineProcess);
                    
                    // Give the engine a moment to start up
                    await Task.Delay(2000);
                }
                
                // Start all other processes in parallel
                var otherProcesses = _processes.Where(p => p.ViewModel.Name != "Zooscape Engine").ToList();
                var startTasks = new List<Task>();
                
                foreach (var managedProcess in otherProcesses)
                {
                    if (managedProcess.ViewModel.Status.Contains("Failed") || managedProcess.ViewModel.Status.Contains("Error"))
                    {
                        Debug.WriteLine($"Skipping {managedProcess.ViewModel.Name} due to build failure");
                        Console.WriteLine($"⚠️ Skipping {managedProcess.ViewModel.Name} due to build failure");
                        continue;
                    }

                    Console.WriteLine($"Queuing start for process: {managedProcess.ViewModel.Name}");
                    // Start each process on a separate background task
                    startTasks.Add(Task.Run(async () => 
                    {
                        // Add a small delay to prevent overwhelming the system
                        await Task.Delay(500);
                        await StartProcessSafelyAsync(managedProcess);
                    }));
                }
                
                // Wait for all processes to start
                Console.WriteLine($"Waiting for {startTasks.Count} processes to start...");
                await Task.WhenAll(startTasks);
                
                Console.WriteLine("=== StartAllAsync completed ===");
                
                // Print final status
                Console.WriteLine("=== Final Process Status ===");
                foreach (var proc in _processes)
                {
                    Console.WriteLine($"  {proc.ViewModel.Name}: {proc.ViewModel.Status}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StartAllAsync failed: {ex}");
                Console.WriteLine($"❌ StartAllAsync failed: {ex}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
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
                
                // Verify working directory exists
                if (!Directory.Exists(managedProcess.StartInfo.WorkingDirectory))
                {
                    var errorMsg = $"Working directory does not exist: {managedProcess.StartInfo.WorkingDirectory}";
                    Console.WriteLine($"❌ ERROR: {errorMsg}");
                    UpdateProcessStatus(managedProcess.ViewModel, "Failed - Directory not found");
                    return;
                }
                
                // Display environment variables
                Console.WriteLine($"Environment Variables:");
                foreach (System.Collections.DictionaryEntry envVar in managedProcess.StartInfo.EnvironmentVariables)
                {
                    Console.WriteLine($"  {envVar.Key} = {envVar.Value}");
                }
                
                UpdateProcessStatus(managedProcess.ViewModel, "Starting...");
                
                // Remove platform-specific compilation and try to start the process directly
                Console.WriteLine($"Attempting to start process...");
                
                // Set up output redirection
                managedProcess.StartInfo.RedirectStandardOutput = true;
                managedProcess.StartInfo.RedirectStandardError = true;
                managedProcess.StartInfo.UseShellExecute = false;
                managedProcess.StartInfo.CreateNoWindow = true;
                
                managedProcess.ProcessInstance = Process.Start(managedProcess.StartInfo);
                
                if (managedProcess.ProcessInstance != null)
                {
                    Console.WriteLine($"✅ Process started successfully - PID: {managedProcess.ProcessInstance.Id}");
                    
                    // Set up real-time output capture
                    managedProcess.ProcessInstance.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Debug.WriteLine($"[{managedProcess.ViewModel.Name} OUTPUT] {e.Data}");
                            if (_dispatcher != null)
                            {
                                _dispatcher.TryEnqueue(() =>
                                {
                                    managedProcess.ViewModel.Logs += $"{e.Data}\n";
                                });
                            }
                            else
                            {
                                managedProcess.ViewModel.Logs += $"{e.Data}\n";
                            }
                        }
                    };
                    
                    managedProcess.ProcessInstance.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Debug.WriteLine($"[{managedProcess.ViewModel.Name} ERROR] {e.Data}");
                            if (_dispatcher != null)
                            {
                                _dispatcher.TryEnqueue(() =>
                                {
                                    managedProcess.ViewModel.Logs += $"ERROR: {e.Data}\n";
                                });
                            }
                            else
                            {
                                managedProcess.ViewModel.Logs += $"ERROR: {e.Data}\n";
                            }
                        }
                    };
                    
                    managedProcess.ProcessInstance.EnableRaisingEvents = true;
                    managedProcess.ProcessInstance.Exited += (sender, args) =>
                    {
                        Console.WriteLine($"Process {managedProcess.ViewModel.Name} exited with code: {managedProcess.ProcessInstance.ExitCode}");
                        UpdateProcessStatus(managedProcess.ViewModel, 
                            managedProcess.ProcessInstance.ExitCode == 0 ? "Stopped" : $"Failed (Exit Code: {managedProcess.ProcessInstance.ExitCode})");
                    };
                    
                    // Start reading output
                    managedProcess.ProcessInstance.BeginOutputReadLine();
                    managedProcess.ProcessInstance.BeginErrorReadLine();
                    
                    UpdateProcessStatus(managedProcess.ViewModel, "Running");
                    Console.WriteLine($"✅ Process {managedProcess.ViewModel.Name} status updated to Running");
                }
                else
                {
                    Console.WriteLine($"❌ Process.Start returned null for {managedProcess.ViewModel.Name}");
                    UpdateProcessStatus(managedProcess.ViewModel, "Failed to start");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start {managedProcess.ViewModel.Name}: {ex}");
                Console.WriteLine($"❌ Exception starting {managedProcess.ViewModel.Name}: {ex.Message}");
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

        private async Task StopProcessOnPortAsync(int port, string serviceName)
        {
#if WINDOWS
            try
            {
                Debug.WriteLine($"Attempting to stop {serviceName} on port {port}...");
                Console.WriteLine($"Attempting to stop {serviceName} on port {port}...");

                // Use netstat to find processes using the port
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netstat",
                        Arguments = "-ano",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var stoppedCount = 0;

                foreach (var line in lines)
                {
                    if (line.Contains($":{port}") && line.Contains("LISTENING"))
                    {
                        var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0 && int.TryParse(parts[^1], out var processId))
                        {
                            try
                            {
                                var targetProcess = Process.GetProcessById(processId);
                                if (targetProcess != null && !targetProcess.HasExited)
                                {
                                    Debug.WriteLine($"Found {serviceName} (PID: {processId}) listening on port {port}. Attempting to stop...");
                                    Console.WriteLine($"Found {serviceName} (PID: {processId}) listening on port {port}. Attempting to stop...");

                                    targetProcess.Kill(true);
                                    
                                    // Wait for process to exit
                                    var maxRetries = 5;
                                    var retryCount = 0;
                                    while (retryCount < maxRetries && !targetProcess.HasExited)
                                    {
                                        await Task.Delay(200);
                                        retryCount++;
                                    }

                                    if (targetProcess.HasExited)
                                    {
                                        Console.WriteLine($"Successfully stopped {serviceName} (PID: {processId}) on port {port}.");
                                        stoppedCount++;
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"Failed to terminate process {processId} after {maxRetries} retries");
                                        Console.WriteLine($"Failed to terminate process {processId} after {maxRetries} retries");
                                    }
                                }
                            }
                            catch (ArgumentException)
                            {
                                // Process not found, already terminated
                                Debug.WriteLine($"Process {processId} not found, already terminated?");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to stop {serviceName} (PID: {processId}) on port {port}: {ex.Message}");
                                Console.WriteLine($"Failed to stop {serviceName} (PID: {processId}) on port {port}: {ex.Message}");
                            }
                        }
                    }
                }

                if (stoppedCount > 0)
                {
                    Debug.WriteLine($"Stopped {stoppedCount} process(es) for {serviceName} on port {port}.");
                    Console.WriteLine($"Stopped {stoppedCount} process(es) for {serviceName} on port {port}.");
                }
                else
                {
                    Debug.WriteLine($"No {serviceName} found listening on port {port}.");
                    Console.WriteLine($"No {serviceName} found listening on port {port}.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to stop processes on port {port}: {ex}");
                Console.WriteLine($"Failed to stop processes on port {port}: {ex}");
            }
#else
            Debug.WriteLine($"Port stopping not supported on this platform for {serviceName} on port {port}");
            Console.WriteLine($"Port stopping not supported on this platform for {serviceName} on port {port}");
#endif
        }

        public async Task StartVisualizerAsync()
        {
            try
            {
                Debug.WriteLine("StartVisualizerAsync called");
                Console.WriteLine("Starting Zooscape Visualizer API and Frontend...");

                // Stop existing visualizer processes first
                await StopProcessOnPortAsync(5252, "Frontend");
                await StopProcessOnPortAsync(5008, "Visualizer API");

                // Wait for ports to be fully released
                Debug.WriteLine("Waiting for ports to be fully released...");
                Console.WriteLine("Waiting for ports to be fully released...");
                await Task.Delay(3000);

                // Get visualizer processes
                var visualizerProcesses = _processes.Where(p => 
                {
                    // Check if this is a visualizer process by looking at the process configuration
                    var config = GetProcessConfig(p.ViewModel.Name);
                    return config?.ProcessType == "Visualizer";
                }).ToList();

                if (visualizerProcesses.Count == 0)
                {
                    Debug.WriteLine("No visualizer processes found in configuration");
                    Console.WriteLine("No visualizer processes found in configuration");
                    return;
                }

                Console.WriteLine($"Starting {visualizerProcesses.Count} visualizer processes...");

                // Start visualizer processes in parallel
                var startTasks = new List<Task>();
                foreach (var managedProcess in visualizerProcesses)
                {
                    Console.WriteLine($"Starting visualizer process: {managedProcess.ViewModel.Name}");
                    startTasks.Add(Task.Run(async () => await StartProcessSafelyAsync(managedProcess)));
                }

                await Task.WhenAll(startTasks);
                Console.WriteLine("Visualizer startup completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StartVisualizerAsync failed: {ex}");
                Console.WriteLine($"StartVisualizerAsync failed: {ex}");
            }
        }

        public async Task StopVisualizerAsync()
        {
            try
            {
                Debug.WriteLine("StopVisualizerAsync called");
                Console.WriteLine("Stopping Zooscape Visualizer...");

                // Stop visualizer processes first
                var visualizerProcesses = _processes.Where(p => 
                {
                    var config = GetProcessConfig(p.ViewModel.Name);
                    return config?.ProcessType == "Visualizer";
                }).ToList();

                foreach (var managedProcess in visualizerProcesses)
                {
                    try
                    {
#if WINDOWS || MACCATALYST || ANDROID
                        if (managedProcess.ProcessInstance != null && !managedProcess.ProcessInstance.HasExited)
                        {
                            managedProcess.ProcessInstance.Kill(true);
                            UpdateProcessStatus(managedProcess.ViewModel, "Stopped");
                        }
#endif
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to stop {managedProcess.ViewModel.Name}: {ex}");
                        UpdateProcessStatus(managedProcess.ViewModel, $"Stop Error: {ex.Message}");
                    }
                }

                // Also stop by port as cleanup
                await StopProcessOnPortAsync(5008, "Visualizer API");
                await StopProcessOnPortAsync(5252, "Frontend");

                Console.WriteLine("Visualizer stopped");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StopVisualizerAsync failed: {ex}");
                Console.WriteLine($"StopVisualizerAsync failed: {ex}");
            }
        }

        private ProcessConfig? GetProcessConfig(string processName)
        {
            // This is a helper method to get the original configuration for a process
            // We'll need to cache the configs during LoadProcessesConfigAsync
            return _processConfigs?.FirstOrDefault(c => c.Name == processName);
        }

        private ProcessConfig[]? _processConfigs;

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
