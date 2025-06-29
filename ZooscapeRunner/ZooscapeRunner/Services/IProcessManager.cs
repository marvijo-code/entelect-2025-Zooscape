using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZooscapeRunner.ViewModels;

namespace ZooscapeRunner.Services
{
    public interface IProcessManager
    {
        event Action<string> RestartTimerTick;

        IEnumerable<ProcessViewModel> GetProcesses();
        Task StartAllAsync();
        Task StopAllAsync();
        Task StartVisualizerAsync();
        Task StopVisualizerAsync();
        void StartAutoRestart();
    }
}
