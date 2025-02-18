using Godot;
using System;

public partial class Music : AudioStreamPlayer
{
    public override void _Process(double delta)
    {
        if (!Playing)
        {
            Play();
        }
    }
}
