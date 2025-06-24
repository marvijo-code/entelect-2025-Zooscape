#nullable disable
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using ZooscapeRunner.Services;

namespace ZooscapeRunner.ViewModels
{
    public class MainViewModel : BindableBase
    {
        private string _autoRestartText = "Auto-restart in: --:--";
        public string AutoRestartText
        {
            get => _autoRestartText;
            set => SetProperty(ref _autoRestartText, value);
        }

        public ObservableCollection<ProcessViewModel> Processes { get; } = new();

        private IProcessManager _processManager;

        public RelayCommand StartAllCommand { get; }
        public RelayCommand StopAllCommand { get; }
        public RelayCommand RestartAllCommand { get; }

        public MainViewModel(IProcessManager processManager)
        {
            _processManager = processManager;
            
            // Initialize commands
            StartAllCommand = new RelayCommand(async _ => await OnStartAll(), _ => _processManager != null);
            StopAllCommand = new RelayCommand(async _ => await OnStopAll(), _ => _processManager != null);
            RestartAllCommand = new RelayCommand(async _ => await OnRestartAll(), _ => _processManager != null);

            // Only initialize if we have a valid process manager
            if (_processManager != null)
            {
                InitializeProcessManager();
            }
        }

        public void UpdateProcessManager(IProcessManager processManager)
        {
            _processManager = processManager;
            if (_processManager != null)
            {
                InitializeProcessManager();
                
                // Refresh command states
                StartAllCommand.RaiseCanExecuteChanged();
                StopAllCommand.RaiseCanExecuteChanged();
                RestartAllCommand.RaiseCanExecuteChanged();
            }
        }

        private void InitializeProcessManager()
        {
            _processManager.RestartTimerTick += (newText) => AutoRestartText = newText;

            // Clear existing processes and load new ones
            Processes.Clear();
            foreach (var process in _processManager.GetProcesses())
            {
                Processes.Add(process);
            }

            _processManager.StartAutoRestart();
        }

        private async Task OnStartAll()
        {
            if (_processManager != null)
                await _processManager.StartAllAsync();
        }

        private async Task OnStopAll()
        {
            if (_processManager != null)
                await _processManager.StopAllAsync();
        }

        private async Task OnRestartAll()
        {
            if (_processManager != null)
            {
                await OnStopAll();
                // In a real scenario, we'd wait for processes to stop before starting.
                await OnStartAll();
            }
        }
    }
}
