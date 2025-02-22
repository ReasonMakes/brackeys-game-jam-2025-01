using Godot;

public partial class Control : Node
{
    [Export] public Godot.Control ControlHealthFill;

    [Export] public Button ButtonQuit;

    [Export] public Player Player;
	[Export] public RobotsControl RobotsControl;

	public int TasksFailed = 0;
	public int MaxFailedTasks = 10;
    public float Difficulty = 1f;
	private float DifficultyIncreaseRatePerFail = 0.75f; //value between 0 and 1, smaller values are a faster rate
    private float DifficultyIncreaseRateContinuous = 0.001f;
    private bool IsAngry = false;

    [Export] private AudioStreamPlayer AudioVABetrayal;
    [Export] private AudioStreamPlayer AudioVATaskFailed;
    [Export] private AudioStreamPlayer AudioVADeathShip;
    private bool IsAudioVADeathShipPlayed = false;
    private bool AIVoiceOverIntroShouldPlay = true;
    private float AIVoiceOverIntroDelay = 5f;

    //HARDWARE
    private double FPSAverageSlowPrevious = 60.0; //assume 60 fps
	public double FPSAverageSlow = 60.0; //assume 60 fps
	private ulong FPSAverageSlowUpdateRate = 100; //how many physics updates must pass before we reconsider the average fps

	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;
		RestartGame();
    }

	public override void _Input(InputEvent @event)
	{
		if (Input.IsActionJustPressed("escape"))
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
            ButtonQuit.Visible = true;
		}

		if (Input.IsActionJustPressed("select") && !ButtonQuit.IsHovered())
		{
			Input.MouseMode = Input.MouseModeEnum.Captured;
            ButtonQuit.Visible = false;

            //IncreaseTasksFailed();
        }

		//Restart game
		if (Input.IsActionJustPressed("restart"))
        {
			RestartGame();
        }
	}

	private void RestartGame()
	{
        Player.Respawn();

        //Reset difficulty to default multiplier
        Difficulty = 1f;
        TasksFailed = 0;
        Player.SetDefaultTasks();

        UpdateShipHealthBar();
        IsAudioVADeathShipPlayed = false;

        //Replay the introduction voice acting
        //AIVoiceOverIntroDelay = 5f;
        //AIVoiceOverIntroShouldPlay = true;

        RobotsControl.RobotsDesiredCount = 0;
        RobotsControl.KillAll();
    }

	public override void _Process(double deltaDouble)
	{
        if (ButtonQuit.ButtonPressed)
        {
            GetTree().Quit();
        }

        float delta = (float)deltaDouble;
        AIVoiceOverIntroDelay = Mathf.Max(0f, AIVoiceOverIntroDelay - delta);
        if (AIVoiceOverIntroDelay <= 0f && AIVoiceOverIntroShouldPlay)
        {
            Player.AudioVAIntro.Play();
            AIVoiceOverIntroShouldPlay = false;
        }
        
        Player.Cam.LabelFPS.Text = $"FPS: {Engine.GetFramesPerSecond()}";

        //Mood scaling
        if (GetVAMood() >= 6)
        {
            RobotsControl.RobotsDesiredCount = 5;
        }
        else if (GetVAMood() >= 5)
        {
            RobotsControl.RobotsDesiredCount = 4;
        }
        else if (GetVAMood() >= 4)
        {
            RobotsControl.RobotsDesiredCount = 3;
        }
        else if (GetVAMood() >= 3)
        {
            RobotsControl.RobotsDesiredCount = 2;
        }
        else if (GetVAMood() >= 2)
        {
            RobotsControl.RobotsDesiredCount = 1;

            //Combat music
            if (Player.IsAlive && Player.Music.StreamActive != Player.Music.StreamCombat)
            {
                Player.Music.StreamActive = Player.Music.StreamCombat;
            }
        }
        else
        {
            RobotsControl.RobotsDesiredCount = 0;
        }
    }

    public override void _PhysicsProcess(double deltaDouble)
	{
		float delta = (float)deltaDouble;

		SetPhysicsUpdateRate();

        //Constantly increase difficulty
        float decayFactor = Mathf.Exp(-DifficultyIncreaseRateContinuous * delta);
        Difficulty = 0f / DifficultyIncreaseRateContinuous * (1f - decayFactor) + (Difficulty * decayFactor);

        //GD.Print(Difficulty);
    }

	public int GetVAMood()
	{
        //Returns a mood from 0 up, with 0 being pleased and 2 being murderous and -1 being the ship has exploded
        //take care that these values fit within MaxFailedTasks as at that point the ship is destroyed
        if (TasksFailed >= MaxFailedTasks)
        {
            return -1;
        }
        if (TasksFailed >= 12)
        {
            return 6;
        }
        else if (TasksFailed >= 10)
        {
            return 5;
        }
        else if (TasksFailed >= 8)
        {
            return 4;
        }
        else if (TasksFailed >= 6)
        {
            return 3;
        }
        else if (TasksFailed >= 3)
        {
            return 2;
        }
        else if (TasksFailed >= 1)
        {
            return 1;
        }
        else
        {
            return 0;
        }
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
		Difficulty *= DifficultyIncreaseRatePerFail;
	}

	public void IncreaseTasksFailed()
	{
		TasksFailed++;
        IncreaseDifficulty();

        if (Player.IsAlive)
        {
            if (TasksFailed > MaxFailedTasks)
            {
                Player.Kill("after the ship's life support failed from too many neglected tasks!");

                if (!IsAudioVADeathShipPlayed)
                {
                    AudioVADeathShip.Play();
                    IsAudioVADeathShipPlayed = true;
                }
            }
            else
            {
                //Betray the player
                if (GetVAMood() >= 2 && !IsAngry)
                {
                    AudioVABetrayal.Play();
                    IsAngry = true;
                }

                //Play task failed VA. Avoid talking over self
                if (!AudioVABetrayal.Playing)
                {
                    AudioVATaskFailed.Play();
                }
            }
        }

        //Update ship health
        UpdateShipHealthBar();

        //GD.Print($"TasksFailed: {TasksFailed}" +
        //	$"\nCombat music: {Player.Music.StreamActive == Player.Music.StreamCombat}" +
        //	$"\nDesired robots: {RobotsControl.RobotsDesiredCount}");
    }

    private void UpdateShipHealthBar()
    {
        //HealthFill.Scale = new(((float)MaxFailedTasks - (float)TasksFailed) / (float)MaxFailedTasks, 1f);
        float textureWidth = ControlHealthFill.GetChild<TextureRect>(0).Size.X;
        float textureHeight = ControlHealthFill.GetChild<TextureRect>(0).Size.Y;
        float healthProportion = Mathf.Max(0f, ((float)MaxFailedTasks - (float)TasksFailed) / (float)MaxFailedTasks);

        ControlHealthFill.Size = Vector2.One;
        ControlHealthFill.Size = new(healthProportion * textureWidth, textureHeight);

        GD.Print($"{textureWidth}, {ControlHealthFill.Size.X}, {healthProportion}");
    }
}
