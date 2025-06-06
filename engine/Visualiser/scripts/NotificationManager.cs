using System;
using System.Threading.Tasks;
using Godot;

public static class NotificationManager
{
    public static async void ShowNotification(
        Node node,
        string notificationText,
        bool isSuccess = true
    )
    {
        var toast = ResourceLoader
            .Load<PackedScene>("res://scenes/menus/popups/toast.tscn")
            .Instantiate();
        var toastHeading = toast.GetNode<Label>("UI/Content/Panel/Heading");
        toastHeading.Text = notificationText;

        var tree = node.GetTree();
        tree.GetRoot().AddChild(toast);

        await Task.Delay(TimeSpan.FromMilliseconds(1500));
        tree.GetRoot().RemoveChild(toast);
    }
}
