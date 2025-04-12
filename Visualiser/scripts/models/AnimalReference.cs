using System;
using Godot;

public class AnimalReference
{
    public Guid ConnectionId { get; set; }
    public Node3D NodeReference { get; set; }
    public Animal AnimalDetail { get; set; }
    public Node ScoreBoardLineItem { get; set; }
}
