using Godot;
using System;
using static Godot.TextServer;

public partial class Robot : CharacterBody3D
{
    [Export] private MeshInstance3D MeshInstance;
    [Export] private GpuParticles3D ParticlesDestroyed;
    [Export] private CollisionShape3D Collider;

    [Export] private NavigationAgent3D NavAgent;
    private float Acceleration = 50f; //75f; //100f;
    private float Drag = 10f;
    private float StopDistance = 2f;
    private float RotationRate = 5f;

    public bool IsAlive = false;

    [Export] private AudioStreamPlayer3D AudioFire;
    [Export] private AudioStreamPlayer3D AudioSpawn;
    [Export] private AudioStreamPlayer3D AudioAmbience;
    [Export] private float AudioAmbienceVolume = 0f;
    [Export] private AudioStreamPlayer3D AudioDestroyed;

    [Export] private Node3D Pool;
    private const float MissileSpawnPeriod = 5f; //Time period in seconds between missile spawns.
                                                 //This can be overridden by the missile's minimum time spent being dead
                                                 //(which is necessary for the missile's death sound to play correctly.)
    private float MissileSpawnTimer = 0f; //no touchy :) Time in seconds remaining until spawning a missile. Counts up from 0f to SpawnPeriod

    public override void _Ready()
    {
        //Set a random ambience start position
        if (AudioAmbience.Stream is AudioStreamMP3 MP3Stream)
        {
            RandomNumberGenerator rng = new();
            AudioAmbience.Play(rng.Randf() * (float)MP3Stream.GetLength());
        }
        else
        {
            GD.PrintErr($"[{GetType().Name}] Error: Stream is not an MP3! Did you use a WAV or AIF by mistake?");
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

            Vector3 accelerationVector = direction * Acceleration;
            if (!control.Player.IsAlive)
            {
                accelerationVector = Vector3.Zero;
            }
            ApplyAccelerationOverTime(accelerationVector, delta);

            MoveAndSlide();

            //Rotate
            UpdateLookDirection(direction, distanceDirect, control.Player.GlobalPosition, delta);

            //Fire missile
            if (control.Player.IsAlive)
            {
                MissileSpawnTimer += delta;
                if (MissileSpawnTimer >= MissileSpawnPeriod)
                {
                    //Find a missile that isn't alive, or don't spawn at all
                    for (int i = 0; i < Pool.GetChildCount(); i++)
                    {
                        Missile missile = Pool.GetChild<Missile>(i);

                        if (!missile.IsAlive && missile.DeadTime >= Missile.DeadPeriod)
                        {
                            //Spawn missile
                            missile.Spawn(GlobalPosition + (Vector3.Up * 1f));

                            //Reset missile spawn timer
                            MissileSpawnTimer = 0f;

                            //Play audio
                            AudioFire.Play();

                            break;
                        }
                    }
                }
            }
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
        if (IsAlive)
        {
            AudioDestroyed.Play();
            ParticlesDestroyed.Emitting = true;

            Velocity = Vector3.Zero;

            Collider.Disabled = true;
            //Visible = false;
            MeshInstance.Visible = false;
            //Position = Vector3.Zero;

            AudioAmbience.VolumeDb = 0f;

            IsAlive = false;
        }
    }

    public void Spawn(Vector3 SpawnPosition)
    {
        AudioSpawn.Play();

        IsAlive = true;
        GlobalPosition = SpawnPosition;

        Collider.Disabled = false;
        Visible = true;
        MeshInstance.Visible = true;

        AudioAmbience.VolumeDb = AudioAmbienceVolume;

        //Initialize missile spawn timer
        MissileSpawnTimer = 0f;
    }
}