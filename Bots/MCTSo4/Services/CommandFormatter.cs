using MCTSo4.Models;

namespace MCTSo4.Services;

public static class CommandFormatter
{
    public static string ToEngineCommand(Move move)
    {
        return move switch
        {
            Move.Up => "up",
            Move.Down => "down",
            Move.Left => "left",
            Move.Right => "right",
            _ => throw new ArgumentOutOfRangeException(nameof(move), move, null),
        };
    }
}
