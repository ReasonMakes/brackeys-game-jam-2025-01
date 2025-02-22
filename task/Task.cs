using Godot;
using System;

public partial class Task : Node3D
{
    private bool InputInteract = false;
    private float InteractDistance = 5f;

    [Export] private Player.TaskType TaskType = Player.TaskType.Cockpit;

    public override void _Input(InputEvent @event)
    {
        InputInteract = Input.IsActionPressed("interact");
    }

    public override void _PhysicsProcess(double delta)
    {
        //Complete task
        Player player = GetNode<Control>(GetTree().Root.GetChild(0).GetPath()).Player;

        if (InputInteract)
        {
            if ((GlobalPosition - player.GlobalPosition).Length() <= InteractDistance)
            {
                //Mark task as completed
                Control control = GetNode<Control>(GetTree().Root.GetChild(0).GetPath());

                if (TaskType == player.TaskCockpit.TaskType)
                {
                    player.TaskCockpit.Reset(control.Difficulty);
                }
                else if (TaskType == player.TaskElectrical.TaskType)
                {
                    player.TaskElectrical.Reset(control.Difficulty);
                }
                else if (TaskType == player.TaskCooler.TaskType)
                {
                    player.TaskCooler.Reset(control.Difficulty);
                }
                else if (TaskType == player.TaskGarden.TaskType)
                {
                    player.TaskGarden.Reset(control.Difficulty);
                }
                else if (TaskType == player.TaskReactor.TaskType)
                {
                    player.TaskReactor.Reset(control.Difficulty);
                }
            }
        }
    }
}