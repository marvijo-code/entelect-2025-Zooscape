using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace ZooscapeRunner.Models
{
    [JsonSerializable(typeof(ProcessConfig))]
    [JsonSerializable(typeof(ProcessConfig[]))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(int[]))]
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    public partial class ProcessConfigJsonContext : JsonSerializerContext
    {
    }
} 