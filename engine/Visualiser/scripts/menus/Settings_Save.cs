using Godot;

public partial class Settings_Save : Button
{
    private TextEdit logsLocationInput { get; set; }

    public override async void _Pressed()
    {
        // var settings = SettingManager.GetGameSettings();

        // logsLocationInput = GetNode<TextEdit>(
        //     "/root/Settings/UI/PageLayout/Center/Options/LogsLocation"
        // );
        // settings.LogsLocation = logsLocationInput.Text;

        // SettingManager.UpdateGameSettings(settings);
        // NotificationManager.ShowNotification(this, "Saved Successfully!");
    }
}
