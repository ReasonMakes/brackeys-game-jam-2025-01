using Godot;
using System;

public partial class Control : Node
{
	[Export] public Player Player;
	[Export] public RobotsControl RobotsControl;

	public int TasksFailed = 0;
	public int MaxFailedTasks = 30;
    public float Difficulty = 1f;
	private float DifficultyIncreaseRate = 0.9f; //value between 0 and 1, smaller values are a faster rate

	//HARDWARE
	private double FPSAverageSlowPrevious = 60.0; //assume 60 fps
	public double FPSAverageSlow = 60.0; //assume 60 fps
	private ulong FPSAverageSlowUpdateRate = 100; //how many physics updates must pass before we reconsider the average fps

	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _Input(InputEvent @event)
	{
		if (Input.IsActionJustPressed("escape"))
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}

		if (Input.IsActionJustPressed("select"))
		{
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}

		//Restart game
		if (Input.IsActionJustPressed("restart"))
		{
			Player.Respawn();

			//Reset difficulty to default multiplier
			Difficulty = 1f;
			TasksFailed = 0;

			RobotsControl.RobotsDesiredCount = 0;
            RobotsControl.KillAll();
		}
	}

	public override void _Process(double delta)
	{
		Player.Cam.LabelFPS.Text = $"FPS: {Engine.GetFramesPerSecond()}";

        //Task-failure scaling - take care that these fit within MaxFailedTasks
        if (TasksFailed >= 15)
        {
            RobotsControl.RobotsDesiredCount = 5;
        }
        else if (TasksFailed >= 10)
        {
            RobotsControl.RobotsDesiredCount = 4;
        }
        else if (TasksFailed >= 6)
        {
            RobotsControl.RobotsDesiredCount = 3;
        }
        else if (TasksFailed >= 3)
        {
            RobotsControl.RobotsDesiredCount = 2;
        }
        else if (TasksFailed >= 1)
        {
            RobotsControl.RobotsDesiredCount = 1;
        }

		//Combat music
        if (TasksFailed >= 1)
        {
            if (Player.IsAlive && Player.Music.StreamActive != Player.Music.StreamCombat)
            {
                Player.Music.StreamActive = Player.Music.StreamCombat;
            }
        }
    }

    public override void _PhysicsProcess(double deltaDouble)
	{
		float delta = (float)deltaDouble;

		SetPhysicsUpdateRate();
	}

	private void SetPhysicsUpdateRate()
	{
		//Hardware
		if (Engine.GetPhysicsFrames() % FPSAverageSlowUpdateRate == 0)
		{
			//Get average fps in slow update
			FPSAverageSlow = (FPSAverageSlowPrevious + Engine.GetFramesPerSecond()) / 2.0;
			FPSAverageSlowPrevious = Engine.GetFramesPerSecond();

			//Set physics to be <= fps
			Engine.PhysicsTicksPerSecond = Mathf.Min((int)FPSAverageSlow, (int)DisplayServer.ScreenGetRefreshRate());

			//GD.Print($"FPSAverageSlow: {FPSAverageSlow}");
			//GD.Print($"DisplayServer.ScreenGetRefreshRate(): {DisplayServer.ScreenGetRefreshRate()}");
		}
	}

	public void IncreaseDifficulty()
	{
		Difficulty *= DifficultyIncreaseRate;
	}

	public void IncreaseTasksFailed()
	{
		TasksFailed++;

		if (TasksFailed > MaxFailedTasks)
		{
			Player.Kill("after the ship's life support failed from too many neglected tasks!");
		}

        GD.Print($"TasksFailed: {TasksFailed}" +
			$"\nCombat music: {Player.Music.StreamActive == Player.Music.StreamCombat}" +
			$"\nDesired robots: {RobotsControl.RobotsDesiredCount}");
    }
}
