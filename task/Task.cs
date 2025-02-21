using Godot;
using System;

public partial class Task : Node3D
{
    private bool InputInteract = false;
    private float InteractDistance = 5f;

    public enum TaskType
    {
        IconCockpit,
        IconElectrical,
        IconCooler,
        IconGarden,
        IconReactor
    }
    [Export] public TaskType Type = TaskType.IconCockpit;

    public override void _Input(InputEvent @event)
    {
        InputInteract = Input.IsActionPressed("interact");
    }

    public override void _PhysicsProcess(double delta)
    {
        Player player = GetNode<Control>(GetTree().Root.GetChild(0).GetPath()).Player;

        if (InputInteract)
        {
            if ((GlobalPosition - player.GlobalPosition).Length() <= InteractDistance)
            {
                if (Type == TaskType.IconCockpit)
                {
                    player.IsTaskCompleteCockpit = true;
                }

                if (Type == TaskType.IconElectrical)
                {
                    player.IsTaskCompleteElectrical = true;
                }

                if (Type == TaskType.IconCooler)
                {
                    player.IsTaskCompleteCooler = true;
                }

                if (Type == TaskType.IconGarden)
                {
                    player.IsTaskCompleteGarden = true;
                }

                if (Type == TaskType.IconReactor)
                {
                    player.IsTaskCompleteReactor = true;
                }
            }
        }
    }
}