using Godot;

public partial class Missile : CharacterBody3D
{
    [Export] private MeshInstance3D MeshInstance;

    public bool IsAlive = false;

    private const float ExplosionRadius = 5f;

    private float Acceleration = 1f;
    private const float Jerk = 20f; //How fast does the acceleration increase
    private const float Drag = 0.15f; //0.2f;
    private const float VerticalLaunchSpeed = 8f; //20f;
    private const float MaxSpeed = 12f; //will not accelerate toward the dot of velocity if Velocity.Length() >= than this value

    private ulong LifeStart = 0;
    private const float LifePeriodThrusters = 0.5f; //Time in seconds until the missile begins thrusting toward the player
    private const ulong LifePeriodMax = 5; //Maximum time in seconds before the missile self-destructs

    [Export] private AudioStreamPlayer3D AudioDestroyed;
    [Export] private AudioStreamPlayer3D AudioAlive;
    [Export] private CollisionShape3D Collider;

    public float DeadTime = 0f; //time the missile's been dead for
    public const float DeadPeriod = 3f; //time the missile needs to be dead for before being possible to spawn again. This is necessary to let the death sound play

    [Export] private GpuParticles3D ParticlesDestroyed;
    [Export] private GpuParticles3D ParticlesThrust;
    public override void _PhysicsProcess(double deltaDouble)
    {
        float delta = (float)deltaDouble;

        if (IsAlive)
        {
            //Get direction
            Control control = GetNode<Control>(GetTree().Root.GetChild(0).GetPath());
            Vector3 direction = control.Player.GlobalPosition - GlobalPosition;

            //Look at player, unless inside of them
            Vector3 lookTargetVector = GlobalTransform.Origin + direction;
            if (lookTargetVector != Vector3.Up)
            {
                LookAt(lookTargetVector);
            }
            
            //Move towards player
            if (Time.GetTicksMsec() - LifeStart >= LifePeriodThrusters * 1000f)
            {
                ApplyJerkOverTime(Jerk, delta);
                //ApplyAccelerationOverTime(direction * Acceleration, delta);

                //Limit speed
                Vector3 velocityHorizontal = new(Velocity.X, 0f, Velocity.Z);
                float thrusterAlignment = velocityHorizontal.Dot(Velocity.Normalized());
                float runAlignmentScaled = Mathf.Clamp(1f - thrusterAlignment / MaxSpeed, 0f, 1f);
                ApplyAccelerationOverTime(direction * (Acceleration * runAlignmentScaled), delta);

                //Limit velocity magnitude to MaxSpeed
                //Velocity = (Velocity.Length() <= MaxSpeed) ? Velocity : Velocity.Normalized() * MaxSpeed;
            }
            else
            {
                //Drag
                ApplyAccelerationOverTime(Vector3.Zero, delta);
            }

            MoveAndSlide();

            //GD.Print($"{Name} Acceleration: {Acceleration}");

            ////Explode on player
            //if (direction.Length() <= ExplosionDistance)
            //{
            //    GD.Print("Missile collided");
            //    Kill();
            //}
            //Explode on any collider
            for (int i = 0; i < GetSlideCollisionCount(); i++)
            {
                KinematicCollision3D collision = GetSlideCollision(i);
                if (collision != null)
                {
                    //Explode and damage nearby entities

                    //GD.Print("Missile collided with: " + collision.GetCollider());

                    //Due to missile speed, it may fly through the collider. This returns it to the point of collision.
                    //Most noticeable for the collision sound. Also relevant for player damage.
                    GlobalPosition = collision.GetPosition();

                    //If close to the player
                    if (direction.Length() <= ExplosionRadius)
                    {
                        control.Player.Kill("by a missile!");
                    }

                    //If close to any robot
                    for (int j = 0; j < control.RobotsControl.Pool.GetChildCount(); j++)
                    {
                        Robot robot = control.RobotsControl.Pool.GetChild(j).GetChild<Robot>(0);

                        if (robot.IsAlive && (robot.GlobalPosition - GlobalPosition).Length() <= ExplosionRadius)
                        {
                            robot.Kill();
                        }
                    }

                    //Self-destruct
                    Kill();

                    break;
                }
            }

            //Alive sound loop
            if (!AudioAlive.Playing)
            {
                AudioAlive.Play();
            }

            //Maximum life time
            if (Time.GetTicksMsec() - LifeStart >= LifePeriodMax * 1000)
            {
                GD.Print("Missile self-destructed");
                Kill();
            }

            //Death time
            DeadTime = 0f;
        }
        else
        {
            AudioAlive.Stop();

            DeadTime += delta;
        }
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

    private void ApplyJerkOverTime(float jerk, float delta)
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
        float decayFactor = Mathf.Exp(-1f * delta);
        Acceleration = jerk / 1f * (1f - decayFactor) + (Acceleration * decayFactor);
    }

    public void Kill()
    {
        if (IsAlive)
        {
            AudioDestroyed.Play();
            ParticlesDestroyed.Emitting = true;
            ParticlesThrust.Emitting = false;

            Acceleration = 1f;
            Velocity = Vector3.Zero;

            Collider.Disabled = true;
            MeshInstance.Visible = false;

            //Node robotsControlPath = GetParent().GetParent().GetParent().GetParent().GetParent();
            //RobotsControl robotsControlScript = GetNode<RobotsControl>(robotsControlPath.GetPath());
            //GlobalPosition = robotsControlScript.GlobalPosition;

            IsAlive = false;
        }
    }

    public void Spawn(Vector3 SpawnPosition)
    {
        ParticlesDestroyed.Emitting = false;
        ParticlesThrust.Emitting = true;

        LifeStart = Time.GetTicksMsec();

        Velocity = Vector3.Up * VerticalLaunchSpeed;
        
        GlobalPosition = SpawnPosition;

        Collider.Disabled = false;
        Visible = true;
        MeshInstance.Visible = true;

        IsAlive = true;
    }
}