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
	[Export] private Label LabelJumpFatigueRecency;
	[Export] private ColorRect RectJumpFatigueRecency;
	[Export] private Label LabelJumpFatigueOnGround;
	[Export] private ColorRect RectJumpFatigueOnGround;

	[Export] private AudioStreamPlayer AudioFootsteps;

	[Export] private CsgBox3D testBox;

	private float MouseSensitivity = 0.001f;

	//RUN
	private bool InputRunForward = false;
	private bool InputRunLeft = false;
	private bool InputRunRight = false;
	private bool InputRunBack = false;

	private const float RunAcceleration = 250f; //allows for fast direction change
	private const float RunAccelerationAirCoefficient = 0.4f; //reduces control while in-air

	private const float RunDragGround = 20f; //LARGER VALUES ARE HIGHER DRAG
	private const float RunDragAir = 0.1f; //LARGER VALUES ARE HIGHER DRAG

	private const float RunMaxSpeedGround = 10f; //run acceleration reduces as top speed is approached
	private const float RunMaxSpeedAir = 5f; //lower top speed in air to keep air movements strictly for direction change rather than to build speed

	private float RunAudioTimer = 0f; //no touchy :)

	[ExportCategory("Seconds between footsteps")]
	[Export] private float RunAudioTimerPeriod = 0.2f; //time in seconds before another footstep sound can be played

	//Jerk allows running acceleration to increase slowly over a few seconds - only applies on-ground
	private const float RunJerkMagnitude = 200f; //the maximum acceleration that jerk imparts on the player once fully developed
	
	private float RunJerkDevelopment = 0f; //no touchy :) develops from 0 up to the value of RunJerkDevelopmentPeriod and is used as the coefficient of RunJerkMagnitude
	private const float RunJerkDevelopmentPeriod = 2f; //time in seconds that jerk takes to fully develop
	
	private const float RunJerkDevelopmentDecayRate = 16f; //How many times faster jerk decreases rather than increases - jerk decay is exponential
	private const float RunJerkDevelopmentDecayRateAir = 4f; //How many times faster jerk decreases rather than increases - jerk decay is exponential

	private const float RunJerkMagnitudeSlideDump = 0.2f; //How much acceleration is dumped from RunJerkDevelopment the instant the player begins a slide

	//CLIMB/WALL-JUMPING/WALL-RUNNING
	private const float ClimbAcceleration = 6f; //Multiple of gravity. Vertical acceleration applied when climbing
	private const float ClimbPeriod = 2f; //time in seconds you can accelerate upwards on the wall for
	private float ClimbRemaining = 0f; //no touchy :)

	private bool CanClimb = false; //no touchy :) Can't climb after jumping off until landing on the ground again

	private const float WallJumpAcceleration = 20f; //instantaneous vertical acceleration

	private bool IsWallRunning = false;
	private float WallRunAcceleration = 1000f; //additional horizontal acceleration applied when wall-running
	private const float WallRunVerticalAcceleration = 1.5f; //Multiple of gravity, proportional to climb remaining. Vertical acceleration applied when wall-running

	//JUMP
	private bool InputTechJump = false;
	private const float JumpAcceleration = 10f; //instantaneous vertical acceleration

	private const float JumpCooldown = 0.2f; //the minimum time in seconds that must pass before the player can jump again (we compare this to JumpFatigueRecencyTimer)

	private float JumpFatigueOnGroundTimer = 0f; //no touchy :) halved immediately after jumping, counts up to JumpFatigueOnGroundTimerPeriod
	private const float JumpFatigueOnGroundTimerPeriod = 0.5f; //time in seconds on the ground until on-ground jump fatigue goes away entirely

	private float JumpFatigueRecencyTimer = 0f; //no touchy :) 0 immediately after jumping, counts up to JumpFatigueRecentTimerPeriod
	private const float JumpFatigueRecencyTimerPeriod = 0.5f; //time in seconds after a jump until recency jump fatigue goes away entirely
	
	private const float JumpFatigueMinimumCoefficient = 0.08f; //the minimum coefficient that jump acceleration can be multiplied by (applies if jump fatigue is extreme)

	//CROUCH/SLIDE
	private bool InputTechCrouchOrSlide = false;
	private bool IsSliding = false;
	private const float RunAccelerationSlidingCoefficient = 0.075f; //Larger values are higher acceleration

	private const float RunDragSlidingCoefficient = 0.05f; //LARGER VALUES ARE HIGHER DRAG - also affects slide-jump speed

	//DASH
	private bool InputTechDash = false;
	private const float DashAcceleration = 300f; //dash acceleration magnitude
	private const float DashAccelerationAirCoefficient = 0.1f; //lower values are lessened acceleration while in the air
	private float DashCooldown = 0f; //no touchy :)
	private const float DashCooldownPeriod = 1f; //5f; //time in seconds until you can use the tech again

	private float DashFadeSpeed = 5.0f; //How fast it fades in/out
	private float DashOpacity = 0.0f; //Start fully transparent
	[Export] private Material DashMaterial; //Store shader material reference

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
		if (InputTechCrouchOrSlide)
		{
			if (!IsSliding)
			{
				RunJerkDevelopment = Mathf.Max(0f, RunJerkDevelopment - RunJerkMagnitudeSlideDump);
				IsSliding = true;
			}
		}
		else
		{
			IsSliding = false;
		}

		bool isClimbingOrWallRunning = IsOnWall() && InputRunForward && ClimbRemaining > 0f && CanClimb;

		//Run
		Vector3 runVector = Run(delta, IsSliding, isClimbingOrWallRunning);
		Velocity += runVector * delta;

		//Wall Climb
		Climb(delta, runVector);

		//Dash
		Dash(delta);

		//Jump
		ProcessJump(delta, Vector3.Up, JumpAcceleration);

		//Gravity
		if (!IsOnFloor())
		{
			Velocity += GetGravity() * delta;
		}
		
		//Drag
		if (IsOnFloor() || isClimbingOrWallRunning)
		{
			//Ground
			float slidingCoefficient = 1f;
			if (IsSliding)
			{
				slidingCoefficient = RunDragSlidingCoefficient;
			}

			Velocity *= Mathf.Clamp(1f - (RunDragGround * slidingCoefficient * delta), 0f, 1f);
		}
		else
		{
			//Air
			Velocity *= Mathf.Clamp(1f - (RunDragAir * delta), 0f, 1f);
		}

		//Apply
		MoveAndSlide();
	}

	private Vector3 Run(float delta, bool isSliding, bool isClimbingOrWallRunning)
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
		//Twitch acceleration
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
			RunJerkDevelopment = Mathf.Min((RunJerkDevelopment + delta) * jerkAlignment, RunJerkDevelopmentPeriod);
		}
		else
		{
			float decayRate = RunJerkDevelopmentDecayRateAir;
			if (IsOnFloor())
			{
				decayRate = RunJerkDevelopmentDecayRate;
			}

			//Decrement
			RunJerkDevelopment = Mathf.Max(
				RunJerkDevelopment - (decayRate * delta),
				0f
			);
			//RunJerkDevelopment = Mathf.Max(
			//    RunJerkDevelopment - ((delta + (RunJerkDevelopmentPeriod - RunJerkDevelopment)) * (decayRate * delta)),
			//    0f
			//);
		}

		//Apply development
		float jerk = (RunJerkDevelopment / RunJerkDevelopmentPeriod) * RunJerkMagnitude;

		//Labels
		LabelJerk.Text = $"Jerk: {jerk:F2}";
		RectJerk.Scale = new(jerk / RunJerkMagnitude, 1f);
		//--


		//--
		//Audio
		if (AudioFootsteps.Playing)
		{
			RunAudioTimer = Mathf.Max(RunAudioTimer - delta, 0f);
		}
		if (RunAudioTimer == 0f && IsOnFloor() && runDirection.Normalized().Length() == 1)
		{
			AudioFootsteps.Play();
			RunAudioTimer = RunAudioTimerPeriod;
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

		//Shader
		float to = 0f;
		if (DashCooldown >= DashCooldownPeriod - (DashCooldownPeriod / 16f))
		{
			to = 1f;
		}
		DashOpacity = Mathf.Lerp(DashOpacity, to, (float)delta * DashFadeSpeed);
		DashMaterial.Set("shader_parameter/opacity", DashOpacity);

		float startLinePosition = 0.6f;
		float dashLinesMovement = startLinePosition + ((1f - startLinePosition) - ((DashCooldown / DashCooldownPeriod) * (1f - startLinePosition)));
		DashMaterial.Set("shader_parameter/movement", dashLinesMovement);
	}

	private void Climb(float delta, Vector3 runVector)
	{
		if (IsOnFloor())
		{
			CanClimb = true;
		}

		//Exhaustion
		if (ClimbRemaining == 0f)
		{
			CanClimb = false;
		}

		//Wall jump
		if (
			!IsOnFloor()
			&& IsOnWall()
			&& GlobalBasis.Z.Dot(GetWallNormal()) >= 0f //looking at the wall
			&& InputTechJump
			//&& !InputRunForward
			&& CanClimb
			&& JumpFatigueRecencyTimer >= JumpCooldown //can jump
		)
		{
			CanClimb = false;

			//Jump up and away from the wall
			Jump(delta, (GetWallNormal() + GetWallNormal() + Vector3.Up).Normalized(), WallJumpAcceleration);
		}

		
		if (IsOnWall() && InputRunForward)
		{
			if (ClimbRemaining > 0f && CanClimb)
			{
				float dotWall = Mathf.Max(GetWallNormal().Dot(GlobalBasis.Z), 0f); // 0 to 1, where 1 is facing the wall

				//Climb
				Velocity += -GetGravity() * (ClimbAcceleration * (ClimbRemaining / ClimbPeriod) * dotWall * delta);

				//Wall-run
				if (!IsOnFloor() && dotWall < 0.75f)
				{
					IsWallRunning = true;

					Vector3 wallTangent = GetWallNormal().Cross(Vector3.Up); //pretty much all the way there
					Vector3 projectedDirection = (wallTangent * runVector.Dot(wallTangent)).Normalized(); //consider which horizontal direction we're going along the wall

					//testBox.Position = new Vector3(Position.X, Position.Y + 1f, Position.Z) + (2f * projectedDirection);

					//Horizontal acceleration
					Velocity += projectedDirection * (WallRunAcceleration * (1f - dotWall) * delta);

					//Vertical acceleration
					Velocity += -GetGravity() * (WallRunVerticalAcceleration * (ClimbRemaining / ClimbPeriod) * dotWall * delta);
				}

				//Decrement
				ClimbRemaining = Mathf.Max(ClimbRemaining - delta, 0f);
			}
		}
		else if (CanClimb)
		{
			//Replenish climb
			ClimbRemaining = Mathf.Min(ClimbRemaining + delta, ClimbPeriod);
		}

		if (!IsOnWall())
		{
			IsWallRunning = false;
		}

		if (IsOnFloor())
		{
			//Replenish climb
			ClimbRemaining = Mathf.Min(ClimbRemaining + delta, ClimbPeriod);
		}

		//Camera
		if (IsWallRunning && !IsOnWall())
		{
			GD.Print("Not on wall while wall running!!");
		}

		if (IsWallRunning)
		{ 
			if (IsOnWall())
			{
				//Set roll
				Cam.Rotation = new Vector3(
					Cam.Rotation.X,
					Cam.Rotation.Y,
					Mathf.LerpAngle(
						Cam.Rotation.Z,
						Mathf.Tau / 16f * Mathf.Sign(GetWallNormal().Dot(-GlobalBasis.X)), //roll rotation
						10f * delta //interpolate speed
					)
				);

				//Get new roll quaternion
				//Quaternion rotation = new(GetWallNormal(), Mathf.Tau * 0.25f);

				//Apply the quaternion
				//Cam.Rotation = (rotation * Cam.Rotation);
			}
		}
		else
		{
			//Reset roll
			Cam.Rotation = new Vector3(
				Cam.Rotation.X,
				Cam.Rotation.Y,
				Mathf.LerpAngle(
					Cam.Rotation.Z,
					0f, //roll rotation
					10f * delta //interpolate speed
				)
			);
		}

		//Label
		LabelClimb.Text = $"Climb: {ClimbRemaining:F2}, CanClimb: {CanClimb}";
		RectClimb.Scale = new(ClimbRemaining / ClimbPeriod, 1f);
	}

	private void ProcessJump(float delta, Vector3 direction, float magnitude)
	{
		//Increment recency timer
		JumpFatigueRecencyTimer = Mathf.Min(JumpFatigueRecencyTimer + delta, JumpFatigueRecencyTimerPeriod);

		if (IsOnFloor())
		{
			//Increment on-ground timer
			JumpFatigueOnGroundTimer = Mathf.Min(JumpFatigueOnGroundTimer + delta, JumpFatigueOnGroundTimerPeriod);

			//Act
			if (InputTechJump && JumpFatigueRecencyTimer >= JumpCooldown)
			{
				//Jump upwards
				Jump(delta, Vector3.Up, JumpAcceleration);

				//Reset timers
				JumpFatigueOnGroundTimer = Mathf.Max(JumpFatigueOnGroundTimer/2f, JumpFatigueMinimumCoefficient);
				JumpFatigueRecencyTimer = 0f;
			}
		}

		//Label
		LabelJumpFatigueRecency.Text = $"Jump fatigue recency: {JumpFatigueRecencyTimer:F2}";
		RectJumpFatigueRecency.Scale = new(JumpFatigueRecencyTimer/JumpFatigueRecencyTimerPeriod, 1f);

		LabelJumpFatigueOnGround.Text = $"Jump fatigue on-ground: {JumpFatigueOnGroundTimer:F2}";
		RectJumpFatigueOnGround.Scale = new(JumpFatigueOnGroundTimer/JumpFatigueOnGroundTimerPeriod, 1f);
	}

	private void Jump(float delta, Vector3 direction, float magnitude)
	{
		//Determine fatigue
		float fatigue = Mathf.Max(
			JumpFatigueMinimumCoefficient,
			Mathf.Min(
				JumpFatigueRecencyTimer / JumpFatigueRecencyTimerPeriod,     //recency jump fatigue
				JumpFatigueOnGroundTimer / JumpFatigueOnGroundTimerPeriod    //on-ground jump fatigue
			)
		);

		//Act
		Velocity += direction * (magnitude * fatigue);
	}
}
