#nullable disable
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using ZooscapeRunner.Services;
using System.Diagnostics;
using System;
using System.Linq;

namespace ZooscapeRunner.ViewModels
{
    public class MainViewModel : BindableBase
    {
        private IProcessManager _processManager;
        private string _autoRestartText = "Auto-restart: Disabled";

        public ObservableCollection<ProcessViewModel> Processes { get; } = new();

        public RelayCommand StartAllCommand { get; }
        public RelayCommand StopAllCommand { get; }
        public RelayCommand RestartAllCommand { get; }
        public RelayCommand StartVisualizerCommand { get; }
        public RelayCommand StopVisualizerCommand { get; }

        // Event to notify UI when StartAll begins
        public event Action StartAllBegun;

        public string AutoRestartText
        {
            get => _autoRestartText;
            set => SetProperty(ref _autoRestartText, value);
        }

        public MainViewModel(IProcessManager processManager)
        {
            _processManager = processManager;

            StartAllCommand = new RelayCommand(async () => await StartAllAsync(), () => _processManager != null);
            StopAllCommand = new RelayCommand(async () => await StopAllAsync(), () => _processManager != null);
            RestartAllCommand = new RelayCommand(async () => await RestartAllAsync(), () => _processManager != null);
            StartVisualizerCommand = new RelayCommand(async () => await StartVisualizerAsync(), () => _processManager != null);
            StopVisualizerCommand = new RelayCommand(async () => await StopVisualizerAsync(), () => _processManager != null);

            if (_processManager != null)
            {
                InitializeProcessManager();
            }
        }

        public void UpdateProcessManager(IProcessManager processManager)
        {
            try
            {
                _processManager = processManager;
                InitializeProcessManager();
                
                // Update command states
                StartAllCommand.RaiseCanExecuteChanged();
                StopAllCommand.RaiseCanExecuteChanged();
                RestartAllCommand.RaiseCanExecuteChanged();
                StartVisualizerCommand.RaiseCanExecuteChanged();
                StopVisualizerCommand.RaiseCanExecuteChanged();
                
                Debug.WriteLine("ProcessManager updated successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to update ProcessManager: {ex}");
                AutoRestartText = $"Error: {ex.Message}";
            }
        }

        private void InitializeProcessManager()
        {
            try
            {
                // Clear existing processes
                Processes.Clear();

                // Add processes from manager
                foreach (var process in _processManager.GetProcesses())
                {
                    Processes.Add(process);
                }

                // Subscribe to timer events
                _processManager.RestartTimerTick += OnRestartTimerTick;

                Debug.WriteLine($"Initialized with {Processes.Count} processes");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize ProcessManager: {ex}");
                AutoRestartText = $"Initialization Error: {ex.Message}";
            }
        }

        private void OnRestartTimerTick(string message)
        {
            try
            {
                AutoRestartText = message;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Timer tick update failed: {ex}");
            }
        }

        private async Task StartAllAsync()
        {
            try
            {
                Console.WriteLine("=== StartAllAsync CALLED ===");
                Debug.WriteLine("=== StartAllAsync CALLED ===");
                
                if (_processManager == null)
                {
                    Debug.WriteLine("ProcessManager is null");
                    Console.WriteLine("ProcessManager is null - CRITICAL ERROR");
                    AutoRestartText = "Error: ProcessManager not initialized";
                    return;
                }

                Debug.WriteLine("Starting all processes...");
                Console.WriteLine("Starting all processes...");
                Console.WriteLine($"ProcessManager has {_processManager.GetProcesses().Count()} processes");
                
                AutoRestartText = "Starting all processes...";
                
                // Notify UI that StartAll has begun so it can auto-select a process for log viewing
                StartAllBegun?.Invoke();
                
                await _processManager.StartAllAsync();
                _processManager.StartAutoRestart();
                
                Debug.WriteLine("Start all completed");
                Console.WriteLine("Start all completed");
                AutoRestartText = "All processes started - Auto-restart enabled";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StartAllAsync failed: {ex}");
                Console.WriteLine($"StartAllAsync failed: {ex}");
                AutoRestartText = $"Start Error: {ex.Message}";
            }
        }

        private async Task StopAllAsync()
        {
            try
            {
                if (_processManager == null)
                {
                    Debug.WriteLine("ProcessManager is null");
                    return;
                }

                Debug.WriteLine("Stopping all processes...");
                await _processManager.StopAllAsync();
                AutoRestartText = "Auto-restart: Disabled";
                Debug.WriteLine("Stop all completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StopAllAsync failed: {ex}");
                AutoRestartText = $"Stop Error: {ex.Message}";
            }
        }

        private async Task RestartAllAsync()
        {
            try
            {
                if (_processManager == null)
                {
                    Debug.WriteLine("ProcessManager is null");
                    return;
                }

                Debug.WriteLine("Restarting all processes...");
                await StopAllAsync();
                await Task.Delay(2000); // Give processes time to stop
                await StartAllAsync();
                Debug.WriteLine("Restart all completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RestartAllAsync failed: {ex}");
                AutoRestartText = $"Restart Error: {ex.Message}";
            }
        }

        private async Task StartVisualizerAsync()
        {
            try
            {
                if (_processManager == null)
                {
                    Debug.WriteLine("ProcessManager is null");
                    AutoRestartText = "Error: ProcessManager not initialized";
                    return;
                }

                Debug.WriteLine("Starting visualizer...");
                AutoRestartText = "Starting visualizer...";
                
                await _processManager.StartVisualizerAsync();
                
                Debug.WriteLine("Visualizer started");
                AutoRestartText = "Visualizer started - API: http://localhost:5008, Frontend: http://localhost:5252";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StartVisualizerAsync failed: {ex}");
                AutoRestartText = $"Visualizer Start Error: {ex.Message}";
            }
        }

        private async Task StopVisualizerAsync()
        {
            try
            {
                if (_processManager == null)
                {
                    Debug.WriteLine("ProcessManager is null");
                    return;
                }

                Debug.WriteLine("Stopping visualizer...");
                AutoRestartText = "Stopping visualizer...";
                
                await _processManager.StopVisualizerAsync();
                
                Debug.WriteLine("Visualizer stopped");
                AutoRestartText = "Visualizer stopped";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StopVisualizerAsync failed: {ex}");
                AutoRestartText = $"Visualizer Stop Error: {ex.Message}";
            }
        }
    }
}
