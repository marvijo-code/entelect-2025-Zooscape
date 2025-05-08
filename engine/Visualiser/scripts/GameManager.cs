using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Microsoft.AspNetCore.SignalR.Client;

public partial class GameManager : Node3D
{
    [ExportGroup("Scene References")]
    [Export]
    PackedScene AnimalScene { get; set; }

    [Export]
    PackedScene ZookeeperScene { get; set; }

    [Export]
    PackedScene PelletScene { get; set; }

    [Export]
    PackedScene WallScene { get; set; }

    [Export]
    PackedScene AnimalSpawnScene { get; set; }

    [Export]
    PackedScene ZookeeperSpawnScene { get; set; }

    [ExportGroup("Configuration")]
    [Export]
    private float GameSpeed { get; set; }

    private Camera3D Camera { get; set; }

    private HubConnection Connection;

    private List<AnimalReference> Animals;
    private List<ZookeeperReference> Zookeepers;
    private List<(Cell, Node3D)> Pellets;

    private TickState State;
    private int StateIndex = 0;
    private bool PlayForward = true;
    private bool Initialized = false;

    public override void _Ready()
    {
        State = new TickState { WorldStates = new List<GameState>() };
        Animals = new List<AnimalReference>();
        Zookeepers = new List<ZookeeperReference>();
        Pellets = new List<(Cell, Node3D)>();

        Camera = GetNode<Camera3D>("Camera3D");

        ManageTimedEvents();
        ManageUiInteractions();
        ManageUiDisplay();
        ManageHubConnection();

        base._Ready();
    }

    private void ManageTimedEvents()
    {
        var timer = GetNode<Timer>("Timer");
        timer.Start(1 / GameSpeed);
        timer.Timeout += ManageTickEvents;
    }

    private void ManageTickEvents()
    {
        ManageUiDisplay();
        VisualiseWorld();
    }

    private void ManageUiInteractions()
    {
        var replayButton = GetNode<Button>(
            "UI/HBoxContainer/VBoxContainer/HBoxContainer/ReplayButton"
        );
        var rewindButton = GetNode<Button>(
            "UI/HBoxContainer/VBoxContainer/HBoxContainer/RewindButton"
        );
        var forwardButton = GetNode<Button>(
            "UI/HBoxContainer/VBoxContainer/HBoxContainer/ForwardButton"
        );

        replayButton.Pressed += OnReplayButtonPressed;
        rewindButton.Pressed += OnRewindButtonPressed;
        forwardButton.Pressed += OnForwardButtonPressed;
    }

    private void ManageUiDisplay()
    {
        var tickNumber = GetNode<Label>("UI/HBoxContainer/VBoxContainer/TickNumber");

        if (StateIndex == 0 && !Initialized)
        {
            var scoreboard = GetNode<VBoxContainer>("UI/HBoxContainer/Scoreboard");
            var scoreboardLineItem = GetNode<HBoxContainer>("UI/HBoxContainer/Scoreboard/LineItem");

            foreach (var animal in Animals)
            {
                var lineItem = scoreboardLineItem.Duplicate();
                scoreboard.AddChild(lineItem);
                animal.ScoreBoardLineItem = lineItem;
            }
        }

        // foreach (var animal in Animals)
        // {
        // 	var lineItem = animal.ScoreBoardLineItem;
        // 	var name = lineItem.GetNode<Label>("PanelContainer/Name");
        // 	var score = lineItem.GetNode<Label>("PanelContainer/Score");
        // 	var captured = lineItem.GetNode<Label>("PanelContainer/Captured");
        // 	var distance = lineItem.GetNode<Label>("PanelContainer/Distance");

        // 	name.Text = animal.AnimalDetail.NickName;
        // 	score.Text = animal.AnimalDetail.Score.ToString();
        // 	captured.Text = animal.AnimalDetail.CapturedCounter.ToString();
        // 	distance.Text = animal.AnimalDetail.DistanceCovered.ToString();
        // }

        tickNumber.Text = $"Tick: {StateIndex}";
    }

    private void OnReplayButtonPressed()
    {
        foreach (var animal in Animals)
        {
            RemoveChild(animal.NodeReference);
        }

        foreach (var zookeeper in Zookeepers)
        {
            RemoveChild(zookeeper.NodeReference);
        }

        foreach (var pellet in Pellets)
        {
            RemoveChild(pellet.Item2);
        }

        Animals = new List<AnimalReference>();
        Zookeepers = new List<ZookeeperReference>();
        Pellets = new List<(Cell, Node3D)>();

        StateIndex = 0;
        PlayForward = true;
        Initialized = false;
        OnForwardButtonPressed();
    }

    private void OnRewindButtonPressed()
    {
        if (!PlayForward)
        {
            return;
        }

        StateIndex--;
        PlayForward = false;
        ManageTickEvents();
    }

    private void OnForwardButtonPressed()
    {
        if (PlayForward)
        {
            return;
        }

        StateIndex++;
        PlayForward = true;
        ManageTickEvents();
    }

    private void ManageHubConnection()
    {
        Connection = new HubConnectionBuilder()
            .WithUrl(new Uri("http://localhost:5000/bothub"))
            .Build();

        if (Connection == null)
        {
            throw new Exception("Connection doesn't exist!");
        }

        if (Connection?.State != HubConnectionState.Connected)
        {
            Connection.On<string>(
                "ClientConnected",
                (connectionId) =>
                {
                    GD.Print($"Client Connected - connectionId: {connectionId}");

                    if (Connection.ConnectionId == connectionId)
                    {
                        Connection.InvokeAsync("RegisterVisualiser");
                    }
                }
            );

            Connection.On<GameState>(
                "GameState",
                (world) =>
                {
                    State.WorldStates.Add(world);
                    GD.Print(
                        $"Visualise Game World - world: {State.WorldStates.Last().Cells.Count}"
                    );
                }
            );

            Connection.StartAsync();
        }
    }

    private void VisualiseWorld()
    {
        if (State == null || StateIndex >= State.WorldStates.Count || StateIndex < 0)
        {
            return;
        }

        if (StateIndex == 0)
        {
            var world = State.WorldStates[StateIndex];
            var mapBase = GetNode<CsgBox3D>("MapBase");

            var x = world.Cells.Max(c => c.X) + 1;
            var y = world.Cells.Max(c => c.Y) + 1;

            mapBase.GlobalPosition = new Vector3(x / 2, -1, y / 2);
            mapBase.Size = new Vector3(x, 1, y);

            Camera.GlobalPosition = new Vector3(
                Camera.GlobalPosition.X,
                Camera.GlobalPosition.Y,
                y / 2
            );
        }

        GD.Print(
            $"empty cells {State.WorldStates[StateIndex].Cells.Where(c => c.Content == CellContent.Empty).Count()}"
        );

        foreach (var zookeeper in State.WorldStates[StateIndex].Zookeepers)
        {
            Node3D obj = null;

            if (StateIndex == 0 && PlayForward && !Initialized)
            {
                obj = (Node3D)ZookeeperScene.Instantiate();
                var label = obj.GetNode<Label>("Node3D/SubViewport/CenterContainer/Label");
                label.Text = zookeeper.NickName;

                AddChild(obj);
                obj.GlobalPosition = new Vector3(zookeeper.X, 0f, zookeeper.Y);
                Zookeepers.Add(
                    new ZookeeperReference
                    {
                        ConnectionId = zookeeper.Id,
                        NodeReference = obj,
                        ZookeeperDetail = zookeeper,
                    }
                );
                continue;
            }

            var bot = Zookeepers.SingleOrDefault(b => b.ConnectionId == zookeeper.Id);
            bot.ZookeeperDetail = zookeeper;
        }

        foreach (var animal in State.WorldStates[StateIndex].Animals)
        {
            Node3D obj = null;

            if (StateIndex == 0 && PlayForward && !Initialized)
            {
                obj = (Node3D)AnimalScene.Instantiate();
                var label = obj.GetNode<Label>("Node3D/SubViewport/CenterContainer/Label");
                label.Text = animal.NickName;

                AddChild(obj);
                obj.GlobalPosition = new Vector3(animal.X, 0f, animal.Y);
                Animals.Add(
                    new AnimalReference
                    {
                        ConnectionId = animal.Id,
                        NodeReference = obj,
                        AnimalDetail = animal,
                    }
                );
                continue;
            }

            var bot = Animals.SingleOrDefault(b => b.ConnectionId == animal.Id);
            bot.AnimalDetail = animal;
        }

        foreach (var cell in State.WorldStates[StateIndex].Cells)
        {
            Node3D obj = null;

            if ((StateIndex == 0 || !PlayForward) && !Initialized)
            {
                switch (cell.Content)
                {
                    case CellContent.Wall:
                        obj = (Node3D)WallScene.Instantiate();
                        break;
                    case CellContent.Pellet:
                        if (!Pellets.Any(p => p.Item1.X == cell.X && p.Item1.Y == cell.Y))
                        {
                            obj = (Node3D)PelletScene.Instantiate();
                            Pellets.Add((cell, obj));
                        }
                        break;
                    case CellContent.AnimalSpawn:
                        obj = (Node3D)AnimalSpawnScene.Instantiate();
                        break;
                    case CellContent.ZookeeperSpawn:
                        obj = (Node3D)ZookeeperSpawnScene.Instantiate();
                        break;
                }
            }

            if (cell.Content == CellContent.Empty)
            {
                var pellet = Pellets.SingleOrDefault(p =>
                    p.Item1.X == cell.X && p.Item1.Y == cell.Y
                );

                if (pellet.Item1 != null)
                {
                    // TODO: Check performance on this function.
                    RemoveChild(pellet.Item2);
                    Pellets.Remove(pellet);
                }
            }

            if (obj == null)
            {
                continue;
            }

            AddChild(obj);
            obj.GlobalPosition = new Vector3(cell.X, 0f, cell.Y);
        }
        GD.Print($"Visualising world");

        if (StateIndex == 0 && !Initialized)
        {
            Initialized = true;
        }

        if (PlayForward)
        {
            StateIndex++;
        }
        else
        {
            StateIndex--;
        }
    }

    public override void _Process(double delta) { }

    public override void _PhysicsProcess(double delta)
    {
        foreach (var zookeeper in Zookeepers)
        {
            MoveCharacter(
                zookeeper.NodeReference,
                new Vector3(zookeeper.ZookeeperDetail.SpawnX, 0f, zookeeper.ZookeeperDetail.SpawnY),
                new Vector3(zookeeper.ZookeeperDetail.X, 0f, zookeeper.ZookeeperDetail.Y),
                delta
            );
        }

        foreach (var bot in Animals)
        {
            MoveCharacter(
                bot.NodeReference,
                new Vector3(bot.AnimalDetail.SpawnX, 0f, bot.AnimalDetail.SpawnY),
                new Vector3(bot.AnimalDetail.X, 0f, bot.AnimalDetail.Y),
                delta
            );
        }

        base._PhysicsProcess(delta);
    }

    private void MoveCharacter(
        Node3D node,
        Vector3 spawnPosition,
        Vector3 targetPosition,
        double delta
    )
    {
        if (node == null)
        {
            return;
        }

        // if (node.GlobalPosition != targetPosition)
        // {
        // 	node.LookAt(targetPosition);
        // }

        if (targetPosition.X == spawnPosition.X && targetPosition.Z == spawnPosition.Z)
        {
            node.GlobalPosition = targetPosition;
            return;
        }

        node.GlobalPosition = node.GlobalPosition.MoveToward(
            targetPosition,
            (float)delta * GameSpeed
        );
    }
}
