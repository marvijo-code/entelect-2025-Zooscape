#nullable disable
using System.Collections.Generic;
using System.Diagnostics;
using ZooscapeRunner.ViewModels;

namespace ZooscapeRunner.Models
{
    public class ManagedProcess
    {
        public ProcessViewModel ViewModel { get; }
        public ProcessStartInfo StartInfo { get; }
        public Process? ProcessInstance { get; set; }

        public ManagedProcess(ProcessViewModel viewModel, ProcessStartInfo startInfo, string workingDirectory, Dictionary<string, string>? environmentVariables)
        {
            ViewModel = viewModel;
            StartInfo = startInfo;

            if (environmentVariables != null)
            {
                foreach (var envVar in environmentVariables)
                {
                    StartInfo.EnvironmentVariables[envVar.Key] = envVar.Value;
                }
            }
        }

        // Keep the old constructor for backward compatibility
        public ManagedProcess(string name, string fileName, string arguments, string workingDirectory, Dictionary<string, string>? environmentVariables)
        {
            ViewModel = new ProcessViewModel { Name = name, Status = "Stopped", ProcessType = "Bot" };

            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (environmentVariables != null)
            {
                foreach (var envVar in environmentVariables)
                {
                    StartInfo.EnvironmentVariables[envVar.Key] = envVar.Value;
                }
            }
        }
    }
}
