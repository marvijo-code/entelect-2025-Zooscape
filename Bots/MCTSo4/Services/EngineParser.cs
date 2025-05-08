using System.Text.Json;
using MCTSo4.Models;

namespace MCTSo4.Services
{
    public static class EngineParser
    {
        public static MCTSGameState Parse(string json)
        {
            // TODO: Map JSON from engine to MCTSGameState
            return JsonSerializer.Deserialize<MCTSGameState>(json)! ?? new MCTSGameState();
        }
    }
}
