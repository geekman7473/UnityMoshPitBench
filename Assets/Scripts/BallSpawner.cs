using UnityEngine;

/// <summary>
/// Spawns pool-ball spheres at a constant rate above the funnel.
/// Each ball has a Rigidbody with appropriate mass, drag, and a bouncy PhysicMaterial.
/// Balls are never destroyed.
/// </summary>
public class BallSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public float spawnRate = 2f;            // balls per second
    public float spawnRadius = 2f;          // random horizontal offset from center
    public float spawnHeight = 12f;         // Y position to spawn at

    [Header("Ball Physics")]
    public float ballRadius = 0.15f;        // pool ball ~57mm diameter
    public float ballMass = 0.17f;          // pool ball ~170g
    public float angularDrag = 0.5f;        // rolling resistance
    public float linearDrag = 0.05f;        // small air resistance
    public float bounciness = 0.5f;
    public float dynamicFriction = 0.4f;
    public float staticFriction = 0.4f;

    [Header("Appearance")]
    public Color[] ballColors = new Color[]
    {
        new Color(0.95f, 0.85f, 0.1f, 1f),   // yellow
        new Color(0.1f, 0.2f, 0.8f, 1f),     // blue
        new Color(0.9f, 0.15f, 0.1f, 1f),    // red
        new Color(0.5f, 0.05f, 0.6f, 1f),    // purple
        new Color(0.95f, 0.5f, 0.05f, 1f),   // orange
        new Color(0.1f, 0.6f, 0.2f, 1f),     // green
        new Color(0.55f, 0.1f, 0.1f, 1f),    // maroon/dark red
        new Color(0.1f, 0.1f, 0.1f, 1f),     // black (8-ball)
    };

    float spawnTimer;
    PhysicsMaterial ballPhysicsMaterial; // Unity 6 uses PhysicsMaterial (not PhysicMaterial)

    void Start()
    {
        // Create a shared physics material for all balls
        ballPhysicsMaterial = new PhysicsMaterial("PoolBallPhysics");
        ballPhysicsMaterial.bounciness = bounciness;
        ballPhysicsMaterial.dynamicFriction = dynamicFriction;
        ballPhysicsMaterial.staticFriction = staticFriction;
        ballPhysicsMaterial.bounceCombine = PhysicsMaterialCombine.Maximum;
        ballPhysicsMaterial.frictionCombine = PhysicsMaterialCombine.Average;
    }

    void Update()
    {
        spawnTimer += Time.deltaTime;
        float interval = 1f / spawnRate;

        while (spawnTimer >= interval)
        {
            spawnTimer -= interval;
            SpawnBall();
        }
    }

    void SpawnBall()
    {
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "PoolBall";
        ball.transform.localScale = Vector3.one * ballRadius * 2f;

        // Random position within spawn radius
        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        ball.transform.position = new Vector3(
            transform.position.x + offset.x,
            spawnHeight,
            transform.position.z + offset.y
        );

        // Physics
        Rigidbody rb = ball.AddComponent<Rigidbody>();
        rb.mass = ballMass;
        rb.angularDamping = angularDrag;
        rb.linearDamping = linearDrag;

        // Apply physics material to collider
        SphereCollider col = ball.GetComponent<SphereCollider>();
        col.material = ballPhysicsMaterial;

        // Random color material
        Renderer rend = ball.GetComponent<Renderer>();
        rend.material = CreateBallMaterial(ballColors[Random.Range(0, ballColors.Length)]);
    }

    Material CreateBallMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.name = "PoolBallMaterial";
        mat.color = color;
        mat.SetFloat("_Metallic", 0.1f);
        mat.SetFloat("_Smoothness", 0.85f); // pool balls are glossy
        return mat;
    }
}
