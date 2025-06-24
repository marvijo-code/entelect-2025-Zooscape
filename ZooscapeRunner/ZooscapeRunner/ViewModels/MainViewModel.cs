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

        private readonly IProcessManager _processManager;

        public ICommand StartAllCommand { get; }
        public ICommand StopAllCommand { get; }
        public ICommand RestartAllCommand { get; }

        public MainViewModel(IProcessManager processManager)
        {
            _processManager = processManager;
            _processManager.RestartTimerTick += (newText) => AutoRestartText = newText;

            StartAllCommand = new RelayCommand(async _ => await OnStartAll());
            StopAllCommand = new RelayCommand(async _ => await OnStopAll());
            RestartAllCommand = new RelayCommand(async _ => await OnRestartAll());

            // Load processes from the service
            foreach (var process in _processManager.GetProcesses())
            {
                Processes.Add(process);
            }

            _processManager.StartAutoRestart();
        }

        private async Task OnStartAll()
        {
            await _processManager.StartAllAsync();
        }

        private async Task OnStopAll()
        {
            await _processManager.StopAllAsync();
        }

        private async Task OnRestartAll()
        {
            await OnStopAll();
            // In a real scenario, we'd wait for processes to stop before starting.
            await OnStartAll();
        }
    }
}
