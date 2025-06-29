#nullable disable
using System.Collections.Generic;

namespace ZooscapeRunner.Models
{
    public class ProcessConfig
    {
        public string Name { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public Dictionary<string, string>? EnvironmentVariables { get; set; }
        public int[]? RequiredPorts { get; set; }
        public string? ProcessType { get; set; }
    }
}
