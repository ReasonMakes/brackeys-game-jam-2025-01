using Godot;
using System;

public partial class Robot : CharacterBody3D
{
    [Export] private NavigationAgent3D NavAgent;
    private float Speed = 2000f;
    private float StopDistance = 5f;

    public override void _PhysicsProcess(double deltaDouble)
    {
        float delta = (float)deltaDouble;

        //Navigation needs to use the 1st frame to sync, so we only begin on the 2nd frame
        if (Engine.GetPhysicsFrames() >= 2)
        {
            Control control = GetNode<Control>(GetTree().Root.GetChild(0).GetPath());

            Vector3 distanceDirect = (control.Player.GlobalPosition - GlobalPosition);

            if (distanceDirect.Length() >= StopDistance)
            {
                NavAgent.TargetPosition = control.Player.GlobalPosition;
            }
            else
            {
                NavAgent.TargetPosition = GlobalPosition;
            }

            Vector3 direction = (NavAgent.GetNextPathPosition() - GlobalPosition).Normalized();

            float magnitude = Speed * delta;
            Velocity = direction * magnitude;

            MoveAndSlide();
        }
    }
}
