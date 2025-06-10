namespace MCTSo4.Models;

using MCTSo4.Enums; // Added for CellContent and ActivePowerUpType

public class Animal
{
    public Guid Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int SpawnX { get; set; }
    public int SpawnY { get; set; }
    public int Score { get; set; }
    public int CapturedCounter { get; set; }
    public int DistanceCovered { get; set; }
    public bool IsViable { get; set; }

    // Power-up related properties
    public CellContent? HeldPowerUp { get; set; } = null;
    public ActivePowerUpType ActivePowerUpEffect { get; set; } = ActivePowerUpType.None;
    public int PowerUpDurationTicks { get; set; } = 0;

    // Score streak property
    public int ScoreStreak { get; set; } = 0;
}
