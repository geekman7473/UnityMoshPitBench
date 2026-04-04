using UnityEngine;

/// <summary>
/// Master bootstrap script. Automatically creates itself via [RuntimeInitializeOnLoadMethod]
/// so no manual editor setup is required. Creates the entire simulation at runtime:
/// ground plane, funnel, stir bar, mountain, ball spawner, and configures the camera.
/// </summary>
public class SceneBootstrap : MonoBehaviour
{
    [Header("Layout Heights")]
    public float groundY = 0f;
    public float mountainPeakY = 4f;
    public float funnelBottomY = 12f;
    public float funnelHeight = 6f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        // Only bootstrap if there isn't one already in the scene
        if (FindAnyObjectByType<SceneBootstrap>() == null)
        {
            GameObject go = new GameObject("GameBootstrap");
            go.AddComponent<SceneBootstrap>();
        }
    }

    void Awake()
    {
        // Increase physics solver quality
        Physics.defaultSolverIterations = 12;
        Physics.defaultSolverVelocityIterations = 4;

        CreateGroundPlane();
        CreateMountain();
        CreateFunnel();
        CreateStirBar();
        CreateBallSpawner();
        SetupCamera();
        CreateBenchmarkManager();
    }

    void CreateBenchmarkManager()
    {
        GameObject bmGo = new GameObject("BenchmarkManager");
        BenchmarkManager bm = bmGo.AddComponent<BenchmarkManager>();
        bm.phase1Duration = 45f;
        bm.phase2Duration = 45f;
        bm.phase3Duration = 45f;
        bm.onTransitionToPhase2 = () => SpaceSceneSetup.Setup();
        bm.onTransitionToPhase3 = () => MazeSceneSetup.Setup();
    }

    void CreateGroundPlane()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, groundY, 0f);
        ground.transform.localScale = new Vector3(100f, 1f, 100f); // effectively infinite

        // Physics material with high friction to slow rolling
        PhysicsMaterial groundPhysMat = new PhysicsMaterial("GroundPhysics");
        groundPhysMat.dynamicFriction = 0.8f;
        groundPhysMat.staticFriction = 0.8f;
        groundPhysMat.bounciness = 0.2f;
        groundPhysMat.bounceCombine = PhysicsMaterialCombine.Minimum;
        groundPhysMat.frictionCombine = PhysicsMaterialCombine.Maximum;

        ground.GetComponent<MeshCollider>().material = groundPhysMat;

        // Green material
        Renderer rend = ground.GetComponent<Renderer>();
        rend.material = CreateOpaqueMaterial("GroundMaterial",
            new Color(0.25f, 0.5f, 0.2f, 1f), 0f, 0.3f);
    }

    void CreateMountain()
    {
        GameObject mountain = new GameObject("Mountain");
        mountain.transform.position = new Vector3(0f, groundY, 0f);

        MountainGenerator gen = mountain.AddComponent<MountainGenerator>();
        gen.peakHeight = mountainPeakY;
        gen.baseRadius = 6f;
        gen.peakRadius = 0.1f;
        gen.segments = 24;
        gen.rings = 12;
        gen.jaggedAmount = 0.6f;
        gen.randomSeed = 12345;
        gen.Generate();
    }

    void CreateFunnel()
    {
        GameObject funnel = new GameObject("Funnel");
        funnel.transform.position = new Vector3(0f, funnelBottomY, 0f);

        FunnelGenerator gen = funnel.AddComponent<FunnelGenerator>();
        gen.topRadius = 5f;
        gen.bottomRadius = 1f;
        gen.height = funnelHeight;
        gen.segments = 32;
        gen.rings = 10;
        gen.Generate();
    }

    void CreateStirBar()
    {
        GameObject stirBar = new GameObject("StirBar");
        // Position just above the funnel bottom opening
        stirBar.transform.position = new Vector3(0f, funnelBottomY + 0.15f, 0f);

        StirBar bar = stirBar.AddComponent<StirBar>();
        bar.barLength = 1.8f;  // slightly less than bottom diameter (2 * bottomRadius)
        bar.barWidth = 0.225f;
        bar.barHeight = 0.45f;
        bar.spinSpeed = 360f;  // degrees per second
        bar.Generate();
    }

    void CreateBallSpawner()
    {
        GameObject spawner = new GameObject("BallSpawner");
        spawner.transform.position = Vector3.zero;

        BallSpawner bs = spawner.AddComponent<BallSpawner>();
        bs.spawnRate = 100f;
        bs.spawnHeight = funnelBottomY + funnelHeight + 2f; // above the funnel top
        bs.ballRadius = 0.15f;
        bs.ballMass = 0.17f;
        bs.angularDrag = 0.5f;
        bs.linearDrag = 0.05f;
    }

    void SetupCamera()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("SceneBootstrap: No MainCamera found!");
            return;
        }

        // Add orbit camera script
        OrbitCamera orbit = mainCam.gameObject.AddComponent<OrbitCamera>();
        float funnelMidY = funnelBottomY + funnelHeight * 0.5f;
        orbit.focalPoint = new Vector3(0f, funnelMidY, 0f);
        orbit.orbitRadius = 20f;
        orbit.orbitSpeed = 15f;
        orbit.baseHeight = funnelMidY + 5f;
        orbit.heightAmplitude = 4f;
        orbit.heightFrequency = 0.15f;
    }

    Material CreateOpaqueMaterial(string name, Color color, float metallic, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.name = name;
        mat.color = color;
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }
}
