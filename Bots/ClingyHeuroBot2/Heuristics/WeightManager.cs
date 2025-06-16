using Marvijo.Zooscape.Bots.Common.Models;
using System.IO;
using System.Text.Json;

namespace ClingyHeuroBot2.Heuristics;

public static class WeightManager
{
    private static readonly HeuristicWeights _instance;

    static WeightManager()
    {
        var weightsJson = File.ReadAllText("heuristic-weights.json");
        _instance = JsonSerializer.Deserialize<HeuristicWeights>(weightsJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new HeuristicWeights();
    }

    public static HeuristicWeights Instance => _instance;
}
