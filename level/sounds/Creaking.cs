using Godot;
using System;

public partial class Creaking : Node
{
    [Export] private AudioStreamPlayer3D Audio;
    private float CreakTimer = 0f;
    private const float CreamTimerPeriodMax = 60f;
    private readonly RandomNumberGenerator rng = new();

    public override void _Process(double delta)
    {
        if (!Audio.Playing && CreakTimer <= 0f)
        {
            Audio.Play();
            CreakTimer = rng.Randf() * CreamTimerPeriodMax;
        }
    }
}
