using Marvijo.Zooscape.Bots.Common.Enums;

namespace Marvijo.Zooscape.Bots.Common.Models;

public class ActivePowerUp
{
    public double Value { get; set; }
    public int TicksRemaining { get; set; }
    public PowerUpType Type { get; set; }
}
