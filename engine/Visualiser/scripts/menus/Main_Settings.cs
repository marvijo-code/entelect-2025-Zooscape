using Godot;

public partial class Main_Settings : Button
{
    public override void _Pressed()
    {
        var scene = ResourceLoader
            .Load<PackedScene>("res://scenes/menus/settings.tscn")
            .Instantiate();
        NavigationManager.NavigateToScene(scene, this);
    }
}
