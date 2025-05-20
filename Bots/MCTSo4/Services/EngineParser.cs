using System.Text.Json;
using MCTSo4.Models;

namespace MCTSo4.Services
{
    public static class EngineParser
    {
        public static MCTSGameState Parse(string json)
        {
            return JsonSerializer.Deserialize<MCTSGameState>(json);
        }
    }
}
