using Godot;

public partial class Settings : Node3D
{
    private TextEdit logsLocationInput { get; set; }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // var settings = SettingManager.GetGameSettings();
        // logsLocationInput = GetNode<TextEdit>("UI/PageLayout/Center/Options/LogsLocation");
        // logsLocationInput.Text = settings.LogsLocation;
    }
}
