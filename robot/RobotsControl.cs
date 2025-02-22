using Godot;

public partial class RobotsControl : Node3D
{
    [Export] private Player Player;
    [Export] private Node3D Pool;
    [Export] private Node3D SpawnPoints;
    private float SpawnTimer = 0f; //counts up and spawns a robots once >= SpawnPeriod. Resets to 0f
    private const float SpawnPeriod = 10f; //40f; //Time period in seconds between robot spawns
    private int robotsDesiredCount = 1; //3; //must be <= pool size (5 at time of writing)

    public override void _Process(double deltaDouble)
    {
        float delta = (float)deltaDouble;

        //Task-failure scaling - take care that these fit within MaxFailedTasks
        Control control = GetNode<Control>(GetTree().Root.GetChild(0).GetPath());
        if (control.TasksFailed <= 20)
        {
            robotsDesiredCount = 5;
        }
        else if (control.TasksFailed <= 15)
        {
            robotsDesiredCount = 4;
        }
        else if (control.TasksFailed <= 10)
        {
            robotsDesiredCount = 3;
        }
        else if (control.TasksFailed <= 6)
        {
            robotsDesiredCount = 2;
        }
        else if (control.TasksFailed <= 3)
        {
            robotsDesiredCount = 1;
        }

        //Spawn robots
        if (SpawnTimer >= SpawnPeriod)
        {
            //Check how many robots are alive right now
            int robotsAliveCount = 0;
            for (int i = 0; i < Pool.GetChildCount(); i++)
            {
                Robot robot = Pool.GetChild(i).GetChild<Robot>(0);

                if (robot.IsAlive)
                {
                    robotsAliveCount++;
                }
            }

            //Find a robot that isn't alive, or don't spawn at all
            if (robotsAliveCount < robotsDesiredCount)
            {
                for (int i = 0; i < Pool.GetChildCount(); i++)
                {
                    Robot robot = Pool.GetChild(i).GetChild<Robot>(0);

                    if (!robot.IsAlive)
                    {
                        robot.Spawn(GetSpawnPoint());

                        break;
                    }
                }
            }

            SpawnTimer = 0f;
        }

        SpawnTimer += delta;
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

    public void KillAll()
    {
        for (int i = 0; i < Pool.GetChildCount(); i++)
        {
            Robot robot = Pool.GetChild(i).GetChild<Robot>(0);

            robot.Kill();
        }
    }
}