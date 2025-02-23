using Godot;
using System;

public partial class CameraNode : Camera3D
{
    [Export] public Label LabelSurvivalTimer;

    [Export] public Label LabelInteractPrompt;

    [Export] public Label LabelDead;

    [Export] public Label LabelHSpeed;
    [Export] public ColorRect RectHSpeed;
    [Export] public Label LabelJerk;
    [Export] public ColorRect RectJerk;
    [Export] public Label LabelDash;
    [Export] public ColorRect RectDash;
    [Export] public ColorRect RectAbilityDash;
    [Export] public Label LabelClimb;
    [Export] public ColorRect RectClimb;
    [Export] public Label LabelJumpFatigueRecency;
    [Export] public ColorRect RectJumpFatigueRecency;
    [Export] public Label LabelJumpFatigueOnGround;
    [Export] public ColorRect RectJumpFatigueOnGround;

    [Export] public TextureRect IconCockpit;
    [Export] public TextureRect IconElectrical;
    [Export] public TextureRect IconCooler;
    [Export] public TextureRect IconGarden;
    [Export] public TextureRect IconReactor;

    [Export] public Label LabelFPS;
    [Export] public Label LabelPhysicsTickRate;

    [Export] public Label LabelDifficulty;

    [Export] public Material DashMaterial; //Store shader material reference
}