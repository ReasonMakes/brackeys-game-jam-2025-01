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
    [Export] private Label LabelJerkDuration;
    [Export] private ColorRect RectJerkDuration;

    private float MouseSensitivity = 0.001f;

    //Run
    private bool InputRunForward = false;
    private bool InputRunLeft = false;
    private bool InputRunRight = false;
    private bool InputRunBack = false;

    private float RunJerkDuration = 0f; //allows running acceleration to increase slowly over a few seconds - only applies on-ground
    private float RunJerkDurationMax = 2f; //time in seconds until jerk becomes static acceleration
    private float RunJerkMagnitude = 100f; //1.5x this value is max jerk
    private float RunJerkDurationDecayRate = 4f; //How many times faster jerk decreases rather than increases

    private float RunAccelerationTwitch = 160f; //allows for fast direction change
    private float RunAirAccelerationTwitchCoefficient = 0.25f; //reduces control while in-air

    private float RunDragGround = 12f; //LARGER VALUES ARE HIGHER DRAG - speed reduces when on-ground

    private float RunMaxSpeedGround = 10f; //run acceleration reduces as top speed is approached
    private float RunMaxSpeedAir = 5f; //lower top speed in air to keep air movements strictly for direction change rather than to build speed

    //Jump
    private bool InputTechJump = false;
    private float JumpAcceleration = 10f;

    //Crouch
    private bool InputTechCrouch = false;

    //Dash
    private bool InputTechDash = false;

    public override void _Input(InputEvent @event)
    {
        //Run Direction
        InputRunForward = Input.IsActionPressed("dir_forward");
        InputRunLeft    = Input.IsActionPressed("dir_left");
        InputRunRight   = Input.IsActionPressed("dir_right");
        InputRunBack    = Input.IsActionPressed("dir_back");

        //Tech
        InputTechJump   = Input.IsActionJustPressed("tech_jump");
        InputTechCrouch = Input.IsActionPressed("tech_crouch");
        InputTechDash   = Input.IsActionPressed("tech_dash");

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

    public override void _PhysicsProcess(double delta)
    {
        //Run
        Vector3 runVector = Run((float)delta);

        //Jump
        if (InputTechJump && IsOnFloor())
        {
            GD.Print("Jump!");
            Velocity += Vector3.Up * JumpAcceleration;
        }

        //Combine all vectors
        Velocity += (runVector + GetGravity()) * (float)delta;

        //Drag
        Velocity *= IsOnFloor() ? Mathf.Clamp(1f - (RunDragGround * (float)delta), 0f, 1f) : 1f;
        //Apply
        MoveAndSlide();
    }

    private Vector3 Run(float delta)
    {
        //Run Direction
        Vector3 runDirection = Vector3.Zero;
        if (InputRunForward) runDirection -= GlobalBasis.Z;
        if (InputRunLeft) runDirection -= GlobalBasis.X;
        if (InputRunRight) runDirection += GlobalBasis.X;
        if (InputRunBack) runDirection += GlobalBasis.Z;
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
        //Ground vs Air Acceleration
        float runDynamicAccelerationTwitch = RunAccelerationTwitch;
        if (!IsOnFloor())
        {
            runDynamicAccelerationTwitch *= RunAirAccelerationTwitchCoefficient;
        }
        //--

        //--
        //Jerk

        //Develop
        //Value from 0.5 to 1 depending on how aligned our running is with our current hVelocity
        float jerkAlignment = Mathf.Clamp(runAlignment / (runDynamicMaxSpeed / 2f), 0f, 1f);
        if (runDirection.Normalized().Length() == 1)
        {
            //Increase acceleration (i.e. make this jerk rather than simply accelerate)
            if (IsOnFloor()) RunJerkDuration = Mathf.Min((RunJerkDuration + (float)delta) * jerkAlignment, RunJerkDurationMax);
        }
        else
        {
            //Decrease
            RunJerkDuration = Mathf.Max(RunJerkDuration - ((float)delta * RunJerkDurationDecayRate), 0f);
        }

        //Apply development
        float jerk = (RunJerkDuration / RunJerkDurationMax) * RunJerkMagnitude;

        //Labels
        LabelJerk.Text = $"Jerk: {jerk:F2}";
        RectJerk.Scale = new(jerk / (RunJerkMagnitude * 1.5f), 1f);

        LabelJerkDuration.Text = $"Jerk Development: {RunJerkDuration:F2}";
        RectJerkDuration.Scale = new(RunJerkDuration / RunJerkDurationMax, 1f);
        //--

        //Add run values together
        float runMagnitude = runDynamicAccelerationTwitch * runAlignmentScaled;
        if (IsOnFloor()) runMagnitude += jerk;
        Vector3 runVector = runDirection * runMagnitude;

        return runVector;
    }
}
