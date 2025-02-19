using Godot;
using System;

public partial class Music : AudioStreamPlayer
{

	[Export] private bool SettingMusicOn = true;
	public override void _Process(double delta)
	{
		if (!Playing && SettingMusicOn)
		{
			Play();
		}
	}
}
