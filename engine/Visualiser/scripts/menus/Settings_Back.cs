using Godot;

public partial class Settings_Back : Button
{
    public override void _Pressed()
    {
        var scene = ResourceLoader.Load<PackedScene>("res://scenes/menus/main.tscn").Instantiate();
        NavigationManager.NavigateToScene(scene, this);
    }
}
