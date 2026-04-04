using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

/// <summary>
/// Sets up Phase 3: Corn Maze with 1000 AI agents.
/// Clears Phase 2 objects, generates a maze, bakes a NavMesh at runtime,
/// spawns 1000 animated humanoids with NavMeshAgent pathfinding to the center.
/// </summary>
public static class MazeSceneSetup
{
    private const int AgentCount = 1000;
    private const float AgentRadius = 0.3f;
    private const float AgentHeight = 1.8f;
    private const float AgentBaseSpeed = 3.5f;
    private const float AgentSpeedVariation = 1.0f; // ± variation

    public static void Setup()
    {
        CleanupPhase2();

        // Re-enable gravity (was disabled for space scene)
        Physics.gravity = new Vector3(0f, -9.81f, 0f);

        // Reset skybox to default
        RenderSettings.skybox = null;

        // Create and generate the maze
        GameObject mazeGo = new GameObject("Maze");
        MazeGenerator maze = mazeGo.AddComponent<MazeGenerator>();
        maze.mazeWidth = 25;
        maze.mazeHeight = 25;
        maze.cellSize = 3f;
        maze.wallHeight = 3f;
        maze.wallThickness = 0.3f;
        maze.seed = 54321;
        maze.Generate();

        // Bake NavMesh at runtime
        BakeNavMesh(mazeGo, maze);

        // Spawn AI agents
        SpawnAgents(maze);

        // Configure camera
        ReconfigureCamera(maze);

        // Configure lighting for indoor/outdoor maze
        ReconfigureLight();
    }

    static void CleanupPhase2()
    {
        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();

        foreach (var root in roots)
        {
            string name = root.name;
            if (name == "Main Camera" || name == "Directional Light" ||
                name == "Global Volume" || name == "GameBootstrap" ||
                name == "BenchmarkManager")
                continue;

            Object.Destroy(root);
        }
    }

    static void BakeNavMesh(GameObject mazeGo, MazeGenerator maze)
    {
        // Add NavMeshSurface to the maze root and bake
        NavMeshSurface surface = mazeGo.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

        // Configure agent settings to match our agents
        surface.overrideTileSize = true;
        surface.tileSize = 64;
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.1f;

        surface.BuildNavMesh();
    }

    static void SpawnAgents(MazeGenerator maze)
    {
        System.Random rng = new System.Random(11111);
        Vector3 destination = maze.CenterWorldPos;

        for (int i = 0; i < AgentCount; i++)
        {
            // Random position in the maze
            Vector3 spawnPos = maze.GetRandomCellPosition(rng);

            // Create animated humanoid
            GameObject agent = ProceduralHumanoid.Create(spawnPos, rng);

            // Add NavMeshAgent
            NavMeshAgent navAgent = agent.AddComponent<NavMeshAgent>();
            navAgent.radius = AgentRadius;
            navAgent.height = AgentHeight;
            navAgent.speed = AgentBaseSpeed + ((float)rng.NextDouble() * 2f - 1f) * AgentSpeedVariation;
            navAgent.angularSpeed = 120f + (float)rng.NextDouble() * 60f;
            navAgent.acceleration = 8f;
            navAgent.stoppingDistance = 1f;
            navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            navAgent.avoidancePriority = rng.Next(0, 100);
            navAgent.autoTraverseOffMeshLink = true;

            // Place on NavMesh — warp to nearest valid position
            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                navAgent.Warp(hit.position);
            }

            // Set destination to maze center
            navAgent.SetDestination(destination);
        }
    }

    static void ReconfigureCamera(MazeGenerator maze)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        // Reset camera clear flags
        mainCam.clearFlags = CameraClearFlags.Skybox;

        OrbitCamera orbit = mainCam.GetComponent<OrbitCamera>();
        if (orbit != null)
        {
            Vector3 center = maze.CenterWorldPos;
            orbit.focalPoint = center + Vector3.up * 2f;
            orbit.orbitRadius = 50f;
            orbit.orbitSpeed = 12f;
            orbit.baseHeight = 35f;
            orbit.heightAmplitude = 15f;
            orbit.heightFrequency = 0.1f;
        }
    }

    static void ReconfigureLight()
    {
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var light in lights)
        {
            if (light.type == LightType.Directional)
            {
                light.intensity = 1.8f;
                light.color = new Color(1f, 0.95f, 0.85f); // warm sunlight
            }
        }

        RenderSettings.ambientLight = new Color(0.3f, 0.28f, 0.22f);
    }
}
