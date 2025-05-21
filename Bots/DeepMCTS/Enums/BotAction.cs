namespace DeepMCTS.Enums
{
    public enum BotAction
    {
        // Values adjusted to be 1-indexed to match ClingyHeuroBot and server expectations
        Up = 1,
        Down = 2,
        Left = 3,
        Right = 4,
        // Note: If the MonteCarlo simulation internally uses 0-3 for array indexing,
        // the conversion to this 1-indexed enum must be handled before sending the command.
    }
}
