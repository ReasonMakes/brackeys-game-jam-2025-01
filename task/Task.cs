using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Runtime;

public partial class Task : Node3D
{
    private bool InputInteract = false;
    private float InteractDistance = 5f;

    [Export] private Player.TaskType TaskType = Player.TaskType.Cockpit;

    [Export] private AudioStreamPlayer AudioVATaskCompletePleased;
    [Export] private AudioStreamPlayer AudioVATaskCompleteNeutral;
    [Export] private AudioStreamPlayer AudioVATaskCompleteAngry;

    public override void _Input(InputEvent @event)
    {
        InputInteract = Input.IsActionJustPressed("interact");
    }

    public override void _PhysicsProcess(double delta)
    {
        //Complete task
        Control control = GetNode<Control>(GetTree().Root.GetChild(0).GetPath());
        Player player = control.Player;

        //Radius
        if ((GlobalPosition - player.GlobalPosition).Length() <= InteractDistance)
        {
            //Raycast
            if (RaycastToPlayerHit(player))
            {
                //Prompt
                control.Player.ShowInteractPrompt();

                //Input bind
                if (InputInteract)
                {
                    //Play VA feedback
                    //Prevent VA talking over itself "I wasn't even finished talking!"
                    if (player.AudioVAIntro.Playing)
                    {
                        player.AudioVAInterrupted.Play();
                        player.AudioVAIntro.Stop();
                    }
                    else if(!player.AudioVAInterrupted.Playing)
                    {
                        if (control.GetVAMood() >= 2)
                        {
                            if (!AudioVATaskCompleteAngry.Playing) AudioVATaskCompleteAngry.Play();
                        }
                        else if (control.GetVAMood() >= 1)
                        {
                            if (!AudioVATaskCompleteNeutral.Playing) AudioVATaskCompleteNeutral.Play();
                        }
                        else if (control.GetVAMood() >= 0)
                        {
                            if (!AudioVATaskCompletePleased.Playing) AudioVATaskCompletePleased.Play();
                        }
                    }
                    
                    //Mark task as completed
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

    private bool RaycastToPlayerHit(Player player)
    {
        PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;

        Vector3 from = GlobalPosition;
        Vector3 to = player.GlobalPosition;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(from, to);

        Dictionary result = spaceState.IntersectRay(query);

        if (result.Count > 0 && result.TryGetValue("collider", out Variant colliderVariant))
        {
            Node3D collider = colliderVariant.As<Node3D>();
            //GD.Print($"Hit object: {collider.Name}, Type: {collider.GetType().Name}");

            if (collider is Player)
            {
                //GD.Print($"Task interact collider: {collider}");

                return true;
            }
        }

        return false;
    }
}