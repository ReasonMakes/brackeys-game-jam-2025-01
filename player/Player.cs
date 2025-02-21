using Godot;
using Godot.NativeInterop;
using System;
using System.Diagnostics;

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

	[Export] private AudioStreamPlayer AudioJump;
	[Export] private AudioStreamPlayer AudioLand;

	[Export] private Control Control;
	[Export] private Label LabelPhysicsTickRate;

	private float MouseSensitivity = 0.001f;

	//AUDIO
	private bool IsInAir = false;

	//RUN
	private bool InputRunForward = false;
	private bool InputRunLeft = false;
	private bool InputRunRight = false;
	private bool InputRunBack = false;

	private const float RunAcceleration = 250f; //allows for fast direction change
	private const float RunAccelerationAirCoefficient = 0.4f; //reduces control while in-air

	private const float RunDragGround = 20f; //8f; //2.890365f; //1040f; //2.890365f; //20f; //higher values are higher drag. Takes any positive value
	private const float RunDragAirCoefficient = 0.01f; //0.05f; //72000f; //200f; //0.05f //higher values are higher drag. Takes any positive value
	private const float RunDragMinMultiplier = 0.1f; //maximum drag the player's velocity will be multiplied by per tick. This is a failsafe for players with slow computers

	private const float RunMaxSpeedGround = 10f; //run acceleration reduces as top speed is approached
	private const float RunMaxSpeedAir = 5f; //lower top speed in air to keep air movements strictly for direction change rather than to build speed

	[Export] private AudioStreamPlayer AudioFootsteps;
	[Export] private AudioStreamPlayer AudioClothes;
	private float RunAudioTimer = 0f; //no touchy :)
	//[ExportCategory("Seconds between footsteps")]
	[Export(PropertyHint.Range, "0,10,")] private float RunAudioTimerPeriod = 0.5f; //time in seconds before another footstep sound can be played

	//Jerk allows running acceleration to increase slowly over a few seconds - only applies on-ground
	private const float RunJerkMagnitude = 200f; //the maximum acceleration that jerk imparts on the player once fully developed
	
	private float RunJerkDevelopment = 0f; //no touchy :) develops from 0 up to the value of RunJerkDevelopmentPeriod and is used as the coefficient of RunJerkMagnitude
	private const float RunJerkDevelopmentRate = 2f; //How quickly jerk increases; i.e. jerk amount; i.e. how quickly acceleration increases
	private const float RunJerkDevelopmentPeriod = 2f; //time in seconds that jerk takes to fully develop
	
	private const float RunJerkDevelopmentDecayRate = 16f; //How many times faster jerk decreases rather than increases - jerk decay is exponential
	private const float RunJerkDevelopmentDecayRateAir = 4f; //How many times faster jerk decreases rather than increases - jerk decay is exponential

	private const float RunJerkMagnitudeSlideDump = 0.2f; //How much acceleration is dumped from RunJerkDevelopment the instant the player begins a slide

	//CLIMB/WALL-JUMPING/WALL-RUNNING
	private bool IsClimbingOrWallRunning = false;

	private const float ClimbAcceleration = 20f; //6f; //Multiple of gravity. Vertical acceleration applied when climbing
	private const float ClimbPeriod = 2f; //time in seconds you can accelerate upwards on the wall for
	private float ClimbRemaining = 0f; //no touchy :)

	private bool CanClimb = false; //no touchy :) Can't climb after jumping off until landing on the ground again

	private const float WallJumpAcceleration = 20f; //instantaneous vertical acceleration

	private bool IsWallRunning = false;
	private float WallRunAcceleration = 6000f; //additional horizontal acceleration applied when wall-running
	private const float ClimbCoefficientWallRunVerticalAcceleration = 0.25f; //1.5f; //Multiple of gravity, proportional to climb remaining. Vertical acceleration applied when wall-running

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
	[Export] private CollisionShape3D ColliderCapsule;
	[Export] private CollisionShape3D ColliderSphere;

	private bool InputTechCrouchOrSlide = false;
	private bool IsSliding = false;
	private const float RunAccelerationSlidingCoefficient = 0.075f; //larger values are higher acceleration

	private const float RunDragSlidingCoefficient = 0.05f; //7200f; //20f; //0.05f; ////higher values are higher drag. Also affects slide-jump speed. Takes any positive value.

	private float CameraYTarget = 1.5f; //no touchy :) Target camera y position
	private float CameraY = 1.5f; //no touchy :) Current camera y position
	private const float CameraYAnimationDuration = 25f; //rate that the camera moves towards the target y position, proportional to the distance

	//DASH
	private bool InputTechDash = false;
	private const float DashAcceleration = 20f; //300f; //dash acceleration magnitude
	private const float DashAccelerationAirCoefficient = 0.05f; //lower values cause lessened aerial acceleration
	private float DashCooldown = 0f; //no touchy :)
	private const float DashCooldownPeriod = 5f; //time in seconds until you can use the tech again

	private float DashFadeSpeed = 5f; //How fast it fades in/out
	private float DashOpacity = 0f; //Start fully transparent
	[Export] private AudioStreamPlayer AudioDash;
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

		//Hardware
		LabelPhysicsTickRate.Text = $"Physics rate: {Engine.PhysicsTicksPerSecond}";

		//Audio
		//Landing
		if (IsInAir && IsOnFloor())
		{
			//Play landing sound
			AudioLand.Play();

			IsInAir = false;
		}
		IsInAir = !IsOnFloor();

		//Slide
		if (InputTechCrouchOrSlide)
		{
			if (!IsSliding)
			{
				//Sliding

				//Instantaneous subtraction from jerk
				RunJerkDevelopment = Mathf.Max(0f, RunJerkDevelopment - RunJerkMagnitudeSlideDump);

				//Switch colliders
				ColliderCapsule.Disabled = true;
				ColliderSphere.Disabled = false;

				//Set camera to new height
				CameraYTarget = 0.5f;

				//Update bool
				IsSliding = true;
			}
		}
		else if (IsSliding)
		{
			//Standing
			//Switch colliders
			ColliderCapsule.Disabled = false;
			ColliderSphere.Disabled = true;

			//Set camera to new height
			CameraYTarget = 1.5f;

			//Update bool
			IsSliding = false;
		}

		//Update camera height
		Cam.Transform = new Transform3D(Cam.Basis, new Vector3(0f, CameraY, 0f));
		CameraY += (CameraYTarget - CameraY) * CameraYAnimationDuration * delta;

		//Update climbing bool
		IsClimbingOrWallRunning = IsOnWall() && InputRunForward && ClimbRemaining > 0f && CanClimb;

		//Run
		Vector3 runVector = Run(delta, IsSliding);
		//Velocity += runVector * delta;
		ApplyAcceleration(runVector, delta);

		//Wall Climb
		Climb(delta, runVector);

		//Dash
		Dash(delta, runVector);

		//Jump
		ProcessJump(delta, Vector3.Up, JumpAcceleration);

		//Gravity
		if (!IsOnFloor())
		{
			//Velocity += GetGravity() * delta;
			ApplyAcceleration(GetGravity(), delta);
		}

		//Apply
		MoveAndSlide();
	}

	private void ApplyAcceleration(Vector3 acceleration, float delta)
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

		//Dynamic drag amount
		float dragComponent;
		if (IsOnFloor() || IsClimbingOrWallRunning)
		{
			//Ground
			float slidingCoefficient = 1f;
			if (IsSliding)
			{
				slidingCoefficient = RunDragSlidingCoefficient;
			}

			dragComponent = RunDragGround * slidingCoefficient;
		}
		else
		{
			//Air
			dragComponent = RunDragGround * RunDragAirCoefficient;
		}

		//Apply drag friction with an exponential decay expression to account for users with throttled physics update rates
		float decayFactor = Mathf.Exp(-dragComponent * delta);
		Velocity = acceleration / dragComponent * (1f - decayFactor) + (Velocity * decayFactor);

		//Vector3 force = new(0f, 0f, 100f * (Time.GetTicksMsec() * 0.0001f)); // Example force applied to the player

		//float dragPerSecond = 20f; // Define friction effect per second
		//float stepDrag = 1f - Mathf.Exp(-dragPerSecond * delta);
		//Velocity *= (1f - stepDrag);

		//Velocity *= Mathf.Exp(-dragComponent * delta);

		//lower values are higher friction. Range: >= 0, < 1
		//float coefficient = 0.9f;
		//Velocity = Velocity * (coefficient * (1f - delta));



		//Velocity *= Mathf.Clamp(1f - (dragComponent * delta), RunDragMinMultiplier, 1f);


		//This does not result in the same jump height across update rates
		//Apply drag friction with an exponential decay expression to account for users with throttled physics update rates
		//Velocity *= Mathf.Exp(-dragComponent * delta);
		//GD.Print(Position.Y);

		//float frictionCoefficient = 2.3f; // Tune this to control friction strength
		//Velocity *= Mathf.Exp(-frictionCoefficient * delta);


		//Velocity *= 0.9f;

		//Velocity /= 1f/delta;
		//float x = 1800f;
		//Velocity *= 0.99f;
		//Velocity *= 1f - (Mathf.Pow(Mathf.E, -x) * delta);
		//Velocity *= 1f - (Mathf.Pow(Mathf.E, -dragComponent) * delta);
		//Velocity *= 1f - Mathf.Pow(Mathf.E, -dragComponent);



		//Velocity *= Mathf.Clamp(1f - (dragComponent * delta), RunDragMaxPerTick, 1f);

		//Higher values of dragComponent result in greater friction
		//Velocity *= 1f - ((dragComponent/(dragComponent + 1f)) * delta);



		//higher framerates need less drag per frame
		//higher framerates have lower delta
		//higher framerate: 0.0028
		//lower framerate: 0.067
		//higher framerate: 1 - 0.0028 = 0.9972
		//lower framerate: 1 - 0.067 = 0.933\


		//x/x+k
		//10/10+10
		//0.5
		//30/30+10
		//0.25



		//Velocity *= Mathf.Clamp(1f - (dragComponent * (1f/360f)), RunDragMaxPerTick, 1f);
		//Velocity *= Mathf.Exp(-dragComponent * delta);
		//Velocity *= Mathf.Exp(dragComponent * delta);
		//Velocity *= Mathf.Clamp(1f - (dragComponent * delta), 0f, 1f);
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
			//Develop jerk - increase acceleration (i.e. make this jerk rather than simply accelerate)
			RunJerkDevelopment = Mathf.Min((RunJerkDevelopment + (delta * RunJerkDevelopmentRate)) * jerkAlignment, RunJerkDevelopmentPeriod);
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
		RunAudioTimer = Mathf.Max(RunAudioTimer - delta, 0f);
		if (
			RunAudioTimer == 0f
			&& (
				(IsOnFloor() && runDirection.Normalized().Length() == 1) || IsWallRunning
			)
		)
		{
			AudioFootsteps.Play();
			AudioClothes.Play();
			RunAudioTimer = RunAudioTimerPeriod;
		}
		//--
		
		//Add run values together
		float runMagnitude = runDynamicAccelerationTwitch * runAlignmentScaled;
		if (IsOnFloor()) runMagnitude += jerk;
		Vector3 runVector = runDirection * runMagnitude;

		return runVector;
	}

	private void Dash(float delta, Vector3 runVector)
	{
		//Act
		if (InputTechDash && DashCooldown == 0f && !IsOnFloor() && ! IsOnWall())
		{
			//Direction
			Vector3 runDirection = runVector.Normalized();
			Vector3 dashDirection = runDirection.Length() == 0f ? -GlobalBasis.Z : runDirection;

			////Variable magnitude
			//float dashMagnitude;
			//if (IsSliding && IsOnFloor())
			//{
			//	//Sliding on ground
			//	dashMagnitude = DashAcceleration * DashAccelerationAirCoefficient;
			//}
			//else if (IsOnFloor())
			//{
			//	//Running on ground
			//	dashMagnitude = DashAcceleration;
			//}
			//else
			//{
			//	//In air
			//    dashMagnitude = DashAcceleration * DashAccelerationAirCoefficient;
			//}

			//Add vector to velocity
			//ApplyAcceleration(dashDirection * dashMagnitude, delta);
			Velocity += dashDirection * DashAcceleration;
			//Velocity += dashDirection * dashMagnitude;

			//Reset cooldown
			DashCooldown = DashCooldownPeriod;

			//Play sound
			AudioDash.Play();
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
		DashOpacity = Mathf.Lerp(DashOpacity, to, delta * DashFadeSpeed);
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
			&& GlobalBasis.Z.Dot(GetWallNormal()) >= 0.75f //looking at the wall
			&& InputTechJump
			//&& !InputRunForward
			&& CanClimb
			&& JumpFatigueRecencyTimer >= JumpCooldown //can jump
		)
		{
			CanClimb = false;

			//Jump up and away from the wall
			Jump((GetWallNormal() + GetWallNormal() + Vector3.Up).Normalized(), WallJumpAcceleration);
		}

		//Climbing or wall-running
		if (IsOnWall() && InputRunForward && !IsSliding)
		{
			if (ClimbRemaining > 0f && CanClimb)
			{
				float dotWall = Mathf.Max(GetWallNormal().Dot(GlobalBasis.Z), 0f); // 0 to 1, where 1 is facing the wall

				//Climb
				Vector3 climbVector = -GetGravity() * (ClimbAcceleration * (ClimbRemaining / ClimbPeriod) * dotWall);
				ApplyAcceleration(climbVector, delta);
				//Velocity += -GetGravity() * (ClimbAcceleration * (ClimbRemaining / ClimbPeriod) * dotWall * delta);

				//Wall-run
				if (!IsOnFloor() && dotWall < 0.75f)
				{
					IsWallRunning = true;

					//Get direction
					Vector3 wallTangent = GetWallNormal().Cross(Vector3.Up); //pretty much all the way there
					Vector3 projectedDirection = (wallTangent * runVector.Dot(wallTangent)).Normalized(); //consider which horizontal direction we're going along the wall
																										  //testBox.Position = new Vector3(Position.X, Position.Y + 1f, Position.Z) + (2f * projectedDirection);

					//Horizontal acceleration
					Vector3 wallRunHorizontalVector = projectedDirection * (WallRunAcceleration * (1f - dotWall));
					ApplyAcceleration(wallRunHorizontalVector, delta);
					//Velocity += projectedDirection * (WallRunAcceleration * (1f - dotWall) * delta);

					//Vertical acceleration
					Vector3 wallRunVerticalVector = -GetGravity() * (ClimbAcceleration * ClimbCoefficientWallRunVerticalAcceleration * (ClimbRemaining / ClimbPeriod) * dotWall);
					ApplyAcceleration(wallRunVerticalVector, delta);
					//Velocity += -GetGravity() * (WallRunVerticalAcceleration * (ClimbRemaining / ClimbPeriod) * dotWall * delta);
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

		//Reset IsWallRunning boolean
		if (!IsOnWall())
		{
			IsWallRunning = false;
		}

		//Replenish climb
		if (IsOnFloor())
		{
			ClimbRemaining = Mathf.Min(ClimbRemaining + delta, ClimbPeriod);
		}

		//Camera
		if (IsWallRunning && !IsOnWall())
		{
			GD.Print("Not on wall while wall running!!");
		}

		//Wall-running camera roll
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
				Jump(Vector3.Up, JumpAcceleration);

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

	private void Jump(Vector3 direction, float magnitude)
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

		//Sound
		AudioJump.Play();
	}
}
