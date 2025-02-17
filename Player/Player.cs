using Godot;
using Godot.NativeInterop;
using System;

public partial class Player : CharacterBody3D
{
    [Export] private Camera3D Cam;

    [Export] private Label LabelHSpeed;
    [Export] private ColorRect RectHSpeed;
    [Export] private Label LabelJerk;
    [Export] private ColorRect RectJerk;
    [Export] private Label LabelDash;
    [Export] private ColorRect RectDash;
    [Export] private Label LabelClimb;
    [Export] private ColorRect RectClimb;

    [Export] private AudioStreamPlayer AudioFootsteps;

    private float MouseSensitivity = 0.001f;

    //RUN
    private bool InputRunForward = false;
    private bool InputRunLeft = false;
    private bool InputRunRight = false;
    private bool InputRunBack = false;

    private const float RunAcceleration = 160f; //allows for fast direction change
    private const float RunAccelerationAirCoefficient = 0.25f; //reduces control while in-air

    private const float RunDragGround = 12f; //LARGER VALUES ARE HIGHER DRAG

    private const float RunMaxSpeedGround = 10f; //run acceleration reduces as top speed is approached
    private const float RunMaxSpeedAir = 5f; //lower top speed in air to keep air movements strictly for direction change rather than to build speed

    //Jerk allows running acceleration to increase slowly over a few seconds - only applies on-ground
    private const float RunJerkMagnitude = 100f; //the maximum acceleration that jerk imparts on the player once fully developed
    private float RunJerkDevelopment = 0f; //no touchy :)
    private const float RunJerkDevelopmentPeriod = 2f; //time in seconds that jerk takes to fully develop
    private const float RunJerkDevelopmentDecayRate = 0.1f; //How many times faster jerk decreases rather than increases

    //CLIMB
    private const float ClimbAcceleration = 2f; //Proportional to gravity. Vertical acceleration applied when climbing
    private const float ClimbWeakenJerk = 0.1f; //how quickly does your acceleration weaken as you climb
    private const float ClimbPeriod = 2f; //time in seconds you can accelerate upwards on the wall for
    private float ClimbRemaining = 0f; //no touchy :)
    private bool CanClimb = false; //can't climb after jumping off until landing on the ground again

    //JUMP
    private bool InputTechJump = false;
    private const float JumpAcceleration = 10f;

    //CROUCH/SLIDE
    private bool InputTechCrouchOrSlide = false;
    private const float RunAccelerationSlidingCoefficient = 0.15f; //Larger values are higher acceleration
    private const float RunDragSlidingCoefficient = 0.3f; //LARGER VALUES ARE HIGHER DRAG

    //DASH
    private bool InputTechDash = false;
    private const float DashAcceleration = 100f; //dash acceleration magnitude
    private const float DashAccelerationAirCoefficient = 0.1f; //lower values are lessened acceleration while in the air
    private float DashCooldown = 0f; //no touchy :)
    private const float DashCooldownPeriod = 5f; //time in seconds until you can use the tech again

    public override void _Input(InputEvent @event)
    {
        //Run Direction
        InputRunForward = Input.IsActionPressed("dir_forward");
        InputRunLeft    = Input.IsActionPressed("dir_left");
        InputRunRight   = Input.IsActionPressed("dir_right");
        InputRunBack    = Input.IsActionPressed("dir_back");

        //Tech
        InputTechJump   = Input.IsActionPressed("tech_jump");
        InputTechCrouchOrSlide = Input.IsActionPressed("tech_crouch");
        InputTechDash   = Input.IsActionJustReleased("tech_dash"); //Mouse wheel only has a released event

        if (Input.IsActionPressed("tech_dash"))
        {
            GD.Print("Dash inputted");
        }

        //Look
        if (@event is InputEventMouseMotion mouseMotion)
        {
            //Yaw
            Rotation = new Vector3(
                Rotation.X,
                Rotation.Y - mouseMotion.Relative.X * MouseSensitivity,
                Rotation.Z
            );

            //Pitch, clamp to straight up or down
            Cam.Rotation = new Vector3(
                Mathf.Clamp(Cam.Rotation.X - mouseMotion.Relative.Y * MouseSensitivity,
                    -0.25f * Mathf.Tau,
                    0.25f * Mathf.Tau
                ),
                Cam.Rotation.Y,
                Cam.Rotation.Z
            );
        }
    }

    public override void _PhysicsProcess(double deltaDouble)
    {
        float delta = (float)deltaDouble;

        //Slide
        bool isSliding = false;
        if (InputTechCrouchOrSlide)
        {
            isSliding = true;
        }

        //Run
        Vector3 runVector = Run(delta, isSliding);
        Velocity += runVector * delta;

        //Wall Climb
        Climb(delta);

        //Dash
        Dash(delta);

        //Jump
        if (InputTechJump && IsOnFloor())
        {
                Velocity += Vector3.Up * JumpAcceleration;
        }

        //Gravity
        if (!IsOnFloor())
        {
            Velocity += GetGravity() * delta;
        }
        
        //Drag
        if (IsOnFloor())
        {
            float slidingCoefficient = 1f;
            if (isSliding)
            {
                slidingCoefficient = RunDragSlidingCoefficient;
            }

            Velocity *= Mathf.Clamp(1f - (RunDragGround * slidingCoefficient * delta), 0f, 1f);
        }

        //Apply
        MoveAndSlide();
    }

    private Vector3 Run(float delta, bool isSliding)
    {
        //Run Direction
        Vector3 runDirection = Vector3.Zero;
        if (InputRunForward)    runDirection -= GlobalBasis.Z;
        if (InputRunLeft)       runDirection -= GlobalBasis.X;
        if (InputRunRight)      runDirection += GlobalBasis.X;
        if (InputRunBack)       runDirection += GlobalBasis.Z;
        runDirection = runDirection.Normalized();


        //--
        //Alignment
        //This prevents accelerating past a max speed in the input direction
        //(a prevalent problem when accelerating in air)
        //while allowing us to maintain responsive air acceleration

        //+ if aligned, - if opposite, 0 if perpendicular
        Vector3 velocityHorizontal = new(Velocity.X, 0f, Velocity.Z);
        LabelHSpeed.Text = $"HSpeed: {velocityHorizontal.Length():F2}";
        RectHSpeed.Scale = new(velocityHorizontal.Length() / RunMaxSpeedGround, 1f);
        float runAlignment = velocityHorizontal.Dot(runDirection);

        //Gradient value from 0 to 1, with:
        // * 0 if aligned and at max speed,
        // * 1 if not aligned,
        // * and a value between if not yet at max speed in that direction
        float runDynamicMaxSpeed = RunMaxSpeedGround;
        if (!IsOnFloor())
        {
            runDynamicMaxSpeed = RunMaxSpeedAir;
        }
        float runAlignmentScaled = Mathf.Clamp(1f - runAlignment / runDynamicMaxSpeed, 0f, 1f);
        //--


        //--
        float runDynamicAccelerationTwitch = RunAcceleration;
        if (!IsOnFloor())
        {
            //Ground vs Air Acceleration
            runDynamicAccelerationTwitch *= RunAccelerationAirCoefficient;
        }
        if (InputTechCrouchOrSlide)
        {
            //Different acceleration when crouched/sliding
            runDynamicAccelerationTwitch *= RunAccelerationSlidingCoefficient;
        }
        //--


        //--
        //Jerk

        //Develop
        //Value from 0.5 to 1 depending on how aligned our running is with our current hVelocity
        float jerkAlignment = Mathf.Clamp(runAlignment / (runDynamicMaxSpeed / 2f), 0f, 1f);
        if (!isSliding && runDirection.Normalized().Length() == 1)
        {
            //Increase acceleration (i.e. make this jerk rather than simply accelerate)
            if (IsOnFloor()) RunJerkDevelopment = Mathf.Min((RunJerkDevelopment + delta) * jerkAlignment, RunJerkDevelopmentPeriod);
        }
        else if (IsOnFloor())
        {
            //Decrement exponentially
            RunJerkDevelopment = Mathf.Max(
                RunJerkDevelopment - ((delta + (RunJerkDevelopmentPeriod - RunJerkDevelopment)) * RunJerkDevelopmentDecayRate),
                0f
            );
        }
        GD.Print($"RunJerkDevelopment: {RunJerkDevelopment}\nRunJerkDevelopmentPeriod: {RunJerkDevelopmentPeriod}\n(RunJerkDevelopmentPeriod - RunJerkDevelopment): {(RunJerkDevelopmentPeriod - RunJerkDevelopment)}");

        //Apply development
        float jerk = (RunJerkDevelopment / RunJerkDevelopmentPeriod) * RunJerkMagnitude;

        //Labels
        LabelJerk.Text = $"Jerk: {jerk:F2}";
        RectJerk.Scale = new(jerk / RunJerkMagnitude, 1f);
        //--


        //--
        //Audio
        if (IsOnFloor() && !AudioFootsteps.Playing && runDirection.Normalized().Length() == 1)
        {
            AudioFootsteps.Play();
        }
        //--
        
        //Add run values together
        float runMagnitude = runDynamicAccelerationTwitch * runAlignmentScaled;
        if (IsOnFloor()) runMagnitude += jerk;
        Vector3 runVector = runDirection * runMagnitude;

        return runVector;
    }

    private void Dash(float delta)
    {
        //Act
        if (InputTechDash && DashCooldown == 0f)
        {
            float dashAccelerationCombined = IsOnFloor() ? DashAcceleration : DashAcceleration * DashAccelerationAirCoefficient;
            Velocity += -GlobalBasis.Z * dashAccelerationCombined;

            //Reset
            DashCooldown = DashCooldownPeriod;
        }

        //Decrement
        DashCooldown = Mathf.Max(DashCooldown - delta, 0f);

        //Label
        LabelDash.Text = $"Dash: {DashCooldown:F2}";
        RectDash.Scale = new(DashCooldown / DashCooldownPeriod, 1f);
    }

    private void Climb(float delta)
    {
        if (IsOnFloor())
        {
            CanClimb = true;
        }

        if (IsOnWall() && InputTechJump)
        {
            CanClimb = false;
            Velocity += (GetWallNormal() + Vector3.Up).Normalized() * JumpAcceleration;
        }

        if (IsOnWall() && InputRunForward && ClimbRemaining > 0f && CanClimb)
        {
            //Climb
            Velocity += -GetGravity() * (ClimbAcceleration * (ClimbRemaining/ClimbPeriod) * delta);

            //Decrement
            ClimbRemaining = Mathf.Max(ClimbRemaining - delta, 0f);
        }
        else
        {
            //Replenish climb
            ClimbRemaining = Mathf.Min(ClimbRemaining + delta, ClimbPeriod);
        }

        //Label
        LabelClimb.Text = $"Climb: {ClimbRemaining:F2}";
        RectClimb.Scale = new(ClimbRemaining / ClimbPeriod, 1f);
    }
}
