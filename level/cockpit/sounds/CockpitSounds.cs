using Godot;
using System;

public partial class CockpitSounds : Node
{
    [Export] private AudioStreamPlayer3D Audio1;
    [Export] private AudioStreamPlayer3D Audio2;
    [Export] private AudioStreamPlayer3D Audio3;

    public override void _Process(double delta)
    {
        if (!Audio1.Playing)
        {
            Audio1.Play();
        }

        if (!Audio2.Playing)
        {
            Audio2.Play();
        }

        if (!Audio3.Playing)
        {
            Audio3.Play();
        }
    }
}
