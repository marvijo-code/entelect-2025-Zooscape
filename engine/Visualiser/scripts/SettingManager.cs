// using Godot;

// public static class SettingManager
// {
//     private static GameSettings gameSettings { get; set; }

//     public static void LoadGameSettings()
//     {
//         gameSettings = new GameSettings();
//         var config = new ConfigFile();
//         var error = config.Load("C://VisualiserConfig/visualiserConfig.cfg");

//         if (error != Error.Ok)
//         {
//             GD.Print($"Error: {error}");
//             return;
//         }

//         foreach (var settings in config.GetSections())
//         {
//             gameSettings.LogsLocation = (string)config.GetValue(settings, "logs_location");
//         }
//     }

//     public static GameSettings GetGameSettings()
//     {
//         if (gameSettings == null)
//         {
//             LoadGameSettings();
//         }

//         return gameSettings;
//     }

//     public static void UpdateGameSettings(GameSettings updatedGameSettings)
//     {
//         gameSettings = updatedGameSettings;
//         var config = new ConfigFile();
//         config.SetValue("Settings", "logs_location", gameSettings.LogsLocation);
//         config.Save("C://VisualiserConfig/visualiserConfig.cfg");
//     }
// }
