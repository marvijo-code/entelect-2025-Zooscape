using System.Collections.Generic;

namespace MCTSBot.Models;

/// <summary>
/// Represents the game state received from the game engine.
/// The actual properties will depend on the Zooscape game engine's API.
/// This is a placeholder and needs to be defined accurately.
/// </summary>
public class EngineGameState
{
    // Example properties - replace with actual game state fields
    public int Tick { get; set; }
    public string? MapString { get; set; } // Or a 2D array, or list of cell objects

    // Player Information (self)
    // public PlayerInfo? MyBot { get; set; }

    // Information about other entities
    // public List<PlayerInfo>? OtherBots { get; set; }
    // public List<EntityInfo>? Zookeepers { get; set; }
    // public List<EntityInfo>? Pellets { get; set; }
    // public List<EntityInfo>? PowerUps { get; set; }

    // Any other relevant game info
    public string? GamePhase { get; set; } // E.g., "InProgress", "Ended"

    // public ScoreInfo? Scores { get; set; }

    public EngineGameState()
    {
        // OtherBots = new List<PlayerInfo>();
        // Zookeepers = new List<EntityInfo>();
        // Pellets = new List<EntityInfo>();
        // PowerUps = new List<EntityInfo>();
    }
}

// Supporting placeholder classes - define these based on actual data structure
/*
public class PlayerInfo
{
    public string? Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Score { get; set; }
    public string? CurrentPowerUp { get; set; }
    public bool IsCaptured { get; set; }
}

public class EntityInfo
{
    public string? Id { get; set; } // Optional, if entities have IDs
    public int X { get; set; }
    public int Y { get; set; }
    public string? Type { get; set; } // E.g., "SpeedBoost", "Invincibility"
}

public class ScoreInfo
{
    // Example: Dictionary of player ID to score
    public Dictionary<string, int>? PlayerScores { get; set; }
}
*/
