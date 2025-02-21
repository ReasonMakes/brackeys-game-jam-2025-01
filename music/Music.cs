using Godot;
using System;

public partial class Music : Node3D
{

	[Export] private bool SettingMusicOn = true;

	[Export] private AudioStreamPlayer StreamNonCombat;
	[Export] private AudioStreamPlayer StreamCombat;

    private AudioStreamPlayer ActiveStream;

    [Export(PropertyHint.Range, "-200,0,")] private float VolumeAll = 0f;
    [Export(PropertyHint.Range, "0,1,")] private float VolumeMultiplierNonCombat = 1f;
    [Export(PropertyHint.Range, "0,1,")] private float VolumeMultiplierCombat = 1f;

    public override void _Ready()
    {
        //Set the volume
        StreamNonCombat.VolumeDb = VolumeAll * VolumeMultiplierCombat;
        StreamCombat.VolumeDb = VolumeAll * VolumeMultiplierCombat;

        //Start with non-combat music
        ActiveStream = StreamNonCombat;

        //Force first song to be element 0
        AudioStreamRandomizer randomizer = (AudioStreamRandomizer)ActiveStream.Stream;
        AudioStream streamElement0 = randomizer.GetStream(0);
        ActiveStream.Stream = streamElement0;
        ActiveStream.Play();
    }

    public override void _Process(double delta)
	{
		if (!ActiveStream.Playing && SettingMusicOn)
		{
            ActiveStream.Play();
		}
	}
}
