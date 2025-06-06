using Godot;

public static class NavigationManager
{
    public static void NavigateToScene(Node scene, Node currentNode)
    {
        var tree = currentNode.GetTree();
        var currentScene = tree.CurrentScene;
        tree.GetRoot().AddChild(scene);
        tree.GetRoot().RemoveChild(currentScene);
        tree.CurrentScene = scene;
    }
}
