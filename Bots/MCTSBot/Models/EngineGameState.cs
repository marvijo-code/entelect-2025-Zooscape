using System.Collections.Generic;

namespace MCTSBot.Models;

/// <summary>
/// Represents the game state received from the game engine.
/// This structure is an assumption of what the engine might provide.
/// </summary>
public class EngineGameState
{
    public int Tick { get; set; }
    public int MaxGameTicks { get; set; } // Max ticks for the entire game

    public List<string> Map { get; set; } = new List<string>(); // Rows of the map
    public int MapWidth { get; set; }
    public int MapHeight { get; set; }

    public PlayerInfo MyBot { get; set; } = new PlayerInfo();
    public List<PlayerInfo> OtherBots { get; set; } = new List<PlayerInfo>();
    public List<EntityInfo> Zookeepers { get; set; } = new List<EntityInfo>();

    // PowerUps, Pellets, EscapeZones are assumed to be represented in the Map strings.
    // Example: 'U' for PowerUp, 'P' for Pellet, 'X' for EscapeZone.

    public string? GamePhase { get; set; } // E.g., "InProgress", "Ended"
}

public class PlayerInfo
{
    public string Id { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Score { get; set; }
    // public string? CurrentPowerUp { get; set; } // Could be added later if needed by MCTS
    // public bool IsCaptured { get; set; } // MCTSGameState determines this based on positions
}

public class EntityInfo // For Zookeepers, potentially other dynamic entities
{
    // public string? Id { get; set; } // Optional, if entities have IDs
    public int X { get; set; }
    public int Y { get; set; }
    // public string? Type { get; set; } // E.g., "ZookeeperTypeA"
}
