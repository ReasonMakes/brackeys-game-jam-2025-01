using Godot;
using System;

public partial class Music : Node3D
{

	[Export] private bool SettingMusicOn = true;

	[Export] public AudioStreamPlayer StreamNonCombat;
	[Export] public AudioStreamPlayer StreamCombat;
	[Export] public AudioStreamPlayer StreamDead;

    public AudioStreamPlayer ActiveStream;

    [Export(PropertyHint.Range, "-200,0,")] private float VolumeAll = 0f;
    [Export(PropertyHint.Range, "0,1,")] private float VolumeMultiplierNonCombat = 1f;
    [Export(PropertyHint.Range, "0,1,")] private float VolumeMultiplierCombat = 1f;
    [Export(PropertyHint.Range, "0,1,")] private float VolumeMultiplierDead = 1f;
    [Export(PropertyHint.Range, "0,1000,")] private float VolumeFadeOutRate = 1f;

    public override void _Ready()
    {
        //Start with non-combat music
        ActiveStream = StreamNonCombat;

        //Force first song to be element 0
        AudioStreamRandomizer randomizer = (AudioStreamRandomizer)ActiveStream.Stream;
        AudioStream streamElement0 = randomizer.GetStream(0);
        ActiveStream.Stream = streamElement0;
        ActiveStream.Play();
    }

    public override void _Process(double deltaDouble)
	{
        float delta = (float)deltaDouble;

		if (!ActiveStream.Playing && SettingMusicOn)
		{
            ActiveStream.Play();
		}

        //Fade out tracks and set volume based on inspector sliders
        //These snap on to 100% volume when active, they only fade out - not in
        if (ActiveStream == StreamNonCombat)
        {
            StreamNonCombat.VolumeDb = VolumeAll * VolumeMultiplierNonCombat;
        }
        else
        {
            StreamNonCombat.VolumeDb -= delta * VolumeFadeOutRate;
        }

        if (ActiveStream == StreamCombat)
        {
            StreamCombat.VolumeDb = VolumeAll * VolumeMultiplierCombat;
        }
        else
        {
            StreamCombat.VolumeDb -= delta * VolumeFadeOutRate;
        }

        if (ActiveStream == StreamDead)
        {
            StreamDead.VolumeDb = VolumeAll * VolumeMultiplierDead;
        }
        else
        {
            StreamDead.VolumeDb -= delta * VolumeFadeOutRate;
        }
    }
}
