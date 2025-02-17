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
    [Export] private AudioStreamPlayer AudioFootsteps;

    private float MouseSensitivity = 0.001f;

    //Run
    private bool InputRunForward = false;
    private bool InputRunLeft = false;
    private bool InputRunRight = false;
    private bool InputRunBack = false;

    //Jerk allows running acceleration to increase slowly over a few seconds - only applies on-ground
    private float RunJerkMagnitude = 100f; //the maximum acceleration that jerk imparts on the player once fully developed
    private float RunJerkDevelopment = 0f; //no touchy :)
    private float RunJerkDevelopmentPeriod = 2f; //time in seconds that jerk takes to fully develop
    private float RunJerkDevelopmentDecayRate = 4f; //How many times faster jerk decreases rather than increases

    private float RunAcceleration = 160f; //allows for fast direction change
    private float RunAccelerationAirCoefficient = 0.25f; //reduces control while in-air

    private float RunDragGround = 12f; //LARGER VALUES ARE HIGHER DRAG

    private float RunAccelerationSlidingCoefficient = 0.25f; //Larger values are higher speed
    private float RunDragSlidingCoefficient = 0.3f; //LARGER VALUES ARE HIGHER DRAG

    private float RunMaxSpeedGround = 10f; //run acceleration reduces as top speed is approached
    private float RunMaxSpeedAir = 5f; //lower top speed in air to keep air movements strictly for direction change rather than to build speed

    //Jump
    private bool InputTechJump = false;
    private float JumpAcceleration = 10f;

    //Crouch
    private bool InputTechCrouchOrSlide = false;

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
        InputTechJump   = Input.IsActionPressed("tech_jump");
        InputTechCrouchOrSlide = Input.IsActionPressed("tech_crouch");
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
        //Slide
        bool isSliding = false;
        if (InputTechCrouchOrSlide)
        {
            isSliding = true;
        }

        //Run
        Vector3 runVector = Run((float)delta, isSliding);
        Velocity += runVector * (float)delta;

        //Jump
        if (InputTechJump && IsOnFloor())
        {
                Velocity += Vector3.Up * JumpAcceleration;
        }

        //Gravity
        if (!IsOnFloor())
        {
            Velocity += GetGravity() * (float)delta;
        }
        
        //Drag
        if (IsOnFloor())
        {
            float slidingCoefficient = 1f;
            if (isSliding)
            {
                slidingCoefficient = RunDragSlidingCoefficient;
            }

            Velocity *= Mathf.Clamp(1f - (RunDragGround * slidingCoefficient * (float)delta), 0f, 1f);
        }

        //Apply
        MoveAndSlide();
    }

    private Vector3 Run(float delta, bool isSliding)
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
        if (runDirection.Normalized().Length() == 1)
        {
            if (!isSliding)
            {
                //Increase acceleration (i.e. make this jerk rather than simply accelerate)
                if (IsOnFloor()) RunJerkDevelopment = Mathf.Min((RunJerkDevelopment + (float)delta) * jerkAlignment, RunJerkDevelopmentPeriod);
            }
        }
        else
        {
            //Decrease
            RunJerkDevelopment = Mathf.Max(RunJerkDevelopment - ((float)delta * RunJerkDevelopmentDecayRate), 0f);
        }

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
}
