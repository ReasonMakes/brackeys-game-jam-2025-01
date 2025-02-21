using Godot;
using System;

public partial class RobotsControl : Node
{
    [Export] private Player Player;
    [Export] private Node3D Pool;
    [Export] private Node3D SpawnPoints;
    private float SpawnPeriod = 2f; //Time period in seconds between robot spawns

    public override void _Process(double delta)
    {
        //Spawn robots
        if (Time.GetTicksMsec() % (SpawnPeriod * 1000f) == 0f)
        {
            //Find a robot that isn't alive, or don't spawn at all
            for (int i = 0; i < Pool.GetChildCount(); i++)
            {
                Robot robot = Pool.GetChild<Robot>(i);

                if (!robot.IsAlive)
                {
                    robot.GlobalPosition = GetSpawnPoint();

                    robot.IsAlive = true;

                    break;
                }
            }
        }
    }

    private Vector3 GetSpawnPoint()
    {
        Node3D closestSpawn = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < SpawnPoints.GetChildCount(); i++)
        {
            Node3D spawnPoint = SpawnPoints.GetChild<Node3D>(i);
            float distance = Player.GlobalPosition.DistanceTo(spawnPoint.GlobalPosition);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestSpawn = spawnPoint;
            }
        }

        return closestSpawn.GlobalPosition;
    }

}
