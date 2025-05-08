using System;
using MCTSBot.Enums;

namespace MCTSBot.Models;

/// <summary>
/// Represents the command structure sent to the game engine.
/// Based on the `botCommand` usage in BasicBot/Program.cs
/// </summary>
public class EngineBotCommand
{
    public Guid BotId { get; set; } // Assuming BotId is part of the command or managed by connection
    public EngineBotAction Action { get; set; }
    public int SequenceNumber { get; set; } // Or any other fields the engine expects

    public EngineBotCommand()
    {
        // Default constructor
    }

    public EngineBotCommand(Guid botId, EngineBotAction action, int sequenceNumber = 0)
    {
        BotId = botId;
        Action = action;
        SequenceNumber = sequenceNumber; // Example, actual fields might differ
    }
}
