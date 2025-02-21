using Godot;
using System;
using static Godot.TextServer;

public partial class Robot : CharacterBody3D
{
    [Export] private CollisionShape3D Collider;

    [Export] private NavigationAgent3D NavAgent;
    private float Acceleration = 100f;
    private float Drag = 10f;
    private float StopDistance = 2f;
    private float RotationRate = 5f;

    public bool IsAlive = false;

    [Export] private AudioStreamPlayer3D Ambience;

    public override void _Ready()
    {
        //Set a random ambience start position
        if (Ambience.Stream is AudioStreamMP3 MP3Stream)
        {
            RandomNumberGenerator rng = new();
            Ambience.Play(rng.Randf() * (float)MP3Stream.GetLength());
        }
        else
        {
            GD.PrintErr($"[{GetType().Name}] Error: Stream is not an MP3! Did you use a WAV or AIF by mistake? I see you, Yetty!! -Scale");
        }
    }

    public override void _PhysicsProcess(double deltaDouble)
    {
        float delta = (float)deltaDouble;

        //We use IsAlive for object pooling
        //Navigation needs to use the 1st frame to sync, so we only begin on the 2nd frame
        if (IsAlive && Engine.GetPhysicsFrames() >= 2)
        {
            //Navigate
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

            ApplyAccelerationOverTime(direction * Acceleration, delta);

            MoveAndSlide();

            //Rotate
            UpdateLookDirection(direction, distanceDirect, control.Player.GlobalPosition, delta);
        }
    }

    private void UpdateLookDirection(Vector3 direction, Vector3 distanceDirect, Vector3 playerPosition, float delta)
    {
        Vector3 lookPosition = direction;
        if (distanceDirect.Length() <= StopDistance)
        {
            lookPosition = (playerPosition - GlobalPosition).Normalized();
        }

        //Rotate
        Quaternion currentRotation = GlobalTransform.Basis.GetRotationQuaternion();
        Quaternion targetRotation = new(Vector3.Up, Mathf.Atan2(lookPosition.X, lookPosition.Z));
        Quaternion newRotation = currentRotation.Slerp(targetRotation, delta * RotationRate);
        GlobalTransform = new Transform3D(new Basis(newRotation), GlobalTransform.Origin);
    }

    private void ApplyAccelerationOverTime(Vector3 acceleration, float delta)
    {
        //DON'T MULTIPLY BY DELTA IN THE ACCELERATION ARGUMENT OF THIS METHOD!

        //Correct example usage:
        //float magnitude = 10f;
        //Vector3 direction = -GlobalBasis.Z;
        //ApplyAcceleration(magnitude * direction, delta);

        //This actually uses a force formula, but we assume the mass is 1, thus it ends up applying acceleration, and the vector is titled acceleration
        //F = ma
        //F = (1)a
        //F = a

        //Apply drag friction with an exponential decay expression to account for users with throttled physics update rates
        float decayFactor = Mathf.Exp(-Drag * delta);
        Velocity = acceleration / Drag * (1f - decayFactor) + (Velocity * decayFactor);
    }

    public void Kill()
    {
        IsAlive = false;
        Collider.Disabled = true;
        Position = Vector3.Zero;
    }

    public void Spawn(Vector3 SpawnPosition)
    {
        IsAlive = true;
        Collider.Disabled = false;
        GlobalPosition = SpawnPosition;
    }
}