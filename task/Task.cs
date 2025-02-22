using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Runtime;

public partial class Task : Node3D
{
    private bool InputInteract = false;
    private float InteractDistance = 5f;

    [Export] private Player.TaskType TaskType = Player.TaskType.Cockpit;

    public override void _Input(InputEvent @event)
    {
        InputInteract = Input.IsActionJustPressed("interact");
    }

    public override void _PhysicsProcess(double delta)
    {
        //Complete task
        Player player = GetNode<Control>(GetTree().Root.GetChild(0).GetPath()).Player;

        if (InputInteract)
        {
            if ((GlobalPosition - player.GlobalPosition).Length() <= InteractDistance)
            {
                if (RaycastToPlayerHit(player))
                {
                    //Mark task as completed
                    Control control = GetNode<Control>(GetTree().Root.GetChild(0).GetPath());

                    if (TaskType == player.TaskCockpit.TaskType)
                    {
                        player.TaskCockpit.Reset(control.Difficulty);
                    }
                    else if (TaskType == player.TaskElectrical.TaskType)
                    {
                        player.TaskElectrical.Reset(control.Difficulty);
                    }
                    else if (TaskType == player.TaskCooler.TaskType)
                    {
                        player.TaskCooler.Reset(control.Difficulty);
                    }
                    else if (TaskType == player.TaskGarden.TaskType)
                    {
                        player.TaskGarden.Reset(control.Difficulty);
                    }
                    else if (TaskType == player.TaskReactor.TaskType)
                    {
                        player.TaskReactor.Reset(control.Difficulty);
                    }
                }
            }
        }
    }

    private bool RaycastToPlayerHit(Player player)
    {
        PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;

        Vector3 from = GlobalPosition;
        Vector3 to = player.GlobalPosition;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(from, to);

        Dictionary result = spaceState.IntersectRay(query);

        if (result.Count > 0 && result.TryGetValue("collider", out Variant colliderVariant))
        {
            Node3D collider = colliderVariant.As<Node3D>();
            //GD.Print($"Hit object: {collider.Name}, Type: {collider.GetType().Name}");

            if (collider is Player)
            {
                GD.Print($"Task interact collider: {collider}");

                return true;
            }
        }

        return false;
    }


    //private void RaycastToPlayer(float radius, Player player)
    //{
    //    Vector3 raycastHitPosition = Vector3.Zero;
    //
    //    //Raycast
    //    Vector3 ray_from = GlobalPosition;
    //    Vector3 ray_to = player.GlobalPosition;
    //    PhysicsRayQueryParameters3D ray_parameters = PhysicsRayQueryParameters3D.Create(ray_from, ray_to);
    //
    //    Dictionary result = GetWorld3D().DirectSpaceState.IntersectRay(ray_parameters);
    //    if (result.Count > 0 && result.ContainsKey("position"))
    //    {
    //        raycastHitPosition = (Vector3)result["position"];
    //    }
    //
    //    //Create response decal
    //
    //
    //    return raycastHitPosition;
    //}

    //private void RaycastToPlayer(float radius)
    //{
    //    PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
    //
    //    //Define the query shape (sphere)
    //    PhysicsShapeQueryParameters3D queryParams = new PhysicsShapeQueryParameters3D();
    //    SphereShape3D sphereShape = new()
    //    {
    //        Radius = radius
    //    };
    //    queryParams.SetShape(sphereShape);
    //    queryParams.Transform = new Transform3D(Basis.Identity, GlobalPosition);
    //    queryParams.CollideWithBodies = true;
    //    queryParams.CollideWithAreas = false;
    //
    //    //Perform the query
    //    var result = spaceState.IntersectShape(queryParams);
    //
    //    // Check each object in the result
    //    foreach (var hitInfo in result)
    //    {
    //        // Get the collider from the object, if any
    //        if (hitInfo.TryGetValue("collider", out Variant colliderVariant))
    //        {
    //            Node3D collider = colliderVariant.As<Node3D>();
    //
    //            if (collider != null)
    //            {
    //                GD.Print($"Found collider {collider}");
    //
    //                if (collider is CharacterBody3D)
    //                {
    //
    //                }
    //
    //                //if (collider is UnitMovement)
    //                //{
    //                //    // Try to get UnitFlags
    //                //    UnitMovement unitMovement = collider as UnitMovement;
    //                //    Node unitFlagsNode = unitMovement.GetParent().GetParent();
    //                //    UnitFlags unitFlags = unitFlagsNode as UnitFlags;
    //                //
    //                //    //GD.Print("UnitFlags found");
    //                //
    //                //    //Only add opposed units to our list
    //                //    if (unitFlags.factionAllegiance != this.unitFlags.factionAllegiance)
    //                //    {
    //                //        GD.Print($"Found unit of aggravating allegiance: {unitFlags.factionAllegiance.Name} != {this.unitFlags.factionAllegiance.Name}");
    //                //
    //                //        unitsInRadius.Add(unitFlags);
    //                //    }
    //                //
    //                //}
    //                //else
    //                //{
    //                //    GD.Print("Collider exists but doesn't have UnitFlags attached");
    //                //}
    //            }
    //        }
    //    }
    //}
}