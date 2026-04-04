using UnityEngine;

/// <summary>
/// Generates a jagged mountain mesh below the funnel opening.
/// Uses a seeded System.Random (seed 12345) for deterministic jagged edges.
/// Peak is at the top, base fans out to the ground, with random radial
/// displacement on each vertex ring to create ridges.
/// </summary>
public class MountainGenerator : MonoBehaviour
{
    [Header("Mountain Dimensions")]
    public float peakHeight = 2f;       // height of the peak above the base
    public float baseRadius = 6f;       // radius at ground level
    public float peakRadius = 0.1f;     // tiny radius at peak (pointed)

    [Header("Mesh Resolution")]
    public int segments = 24;           // around circumference
    public int rings = 12;              // vertical subdivisions

    [Header("Jaggedness")]
    public float jaggedAmount = 0.6f;   // max displacement as fraction of radius
    public int randomSeed = 12345;      // deterministic seed

    [Header("Appearance")]
    public Color mountainColor = new Color(0.45f, 0.32f, 0.18f, 1f); // brown

    public void Generate()
    {
        Mesh mesh = BuildMountainMesh();

        MeshFilter mf = gameObject.AddComponent<MeshFilter>();
        mf.mesh = mesh;

        MeshRenderer mr = gameObject.AddComponent<MeshRenderer>();
        mr.material = CreateMaterial();

        MeshCollider mc = gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        mc.convex = false;
    }

    Mesh BuildMountainMesh()
    {
        System.Random rng = new System.Random(randomSeed);
        Mesh mesh = new Mesh();
        mesh.name = "MountainMesh";

        // +1 for the peak vertex
        int vertCount = (rings + 1) * (segments + 1) + 1;
        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];

        // Build rings from base (ring=0, y=0) to near-peak (ring=rings, y=peakHeight)
        for (int ring = 0; ring <= rings; ring++)
        {
            float t = (float)ring / rings; // 0 = base, 1 = peak
            float y = t * peakHeight;
            float radius = Mathf.Lerp(baseRadius, peakRadius, t);

            // Jaggedness decreases near the peak to keep it pointy
            float jagScale = jaggedAmount * (1f - t * t);

            for (int seg = 0; seg <= segments; seg++)
            {
                float angle = (float)seg / segments * Mathf.PI * 2f;

                // Random radial displacement for jaggedness
                float displacement = 1f + (float)(rng.NextDouble() * 2.0 - 1.0) * jagScale;
                float r = radius * displacement;

                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;

                // Also add some random vertical displacement for ruggedness
                float yOffset = 0f;
                if (ring > 0 && ring < rings) // don't displace base or peak
                {
                    yOffset = (float)(rng.NextDouble() * 2.0 - 1.0) * peakHeight * 0.05f;
                }

                int idx = ring * (segments + 1) + seg;
                vertices[idx] = new Vector3(x, y + yOffset, z);
                uvs[idx] = new Vector2((float)seg / segments, t);
            }
        }

        // Peak vertex
        int peakIdx = (rings + 1) * (segments + 1);
        vertices[peakIdx] = new Vector3(0f, peakHeight, 0f);
        uvs[peakIdx] = new Vector2(0.5f, 1f);

        // Build triangles — outward-facing normals (standard winding)
        int quadTris = rings * segments * 6;
        // No peak cap triangles needed since peak ring is already very small
        int[] triangles = new int[quadTris];
        int tri = 0;

        for (int ring = 0; ring < rings; ring++)
        {
            for (int seg = 0; seg < segments; seg++)
            {
                int current = ring * (segments + 1) + seg;
                int next = current + (segments + 1);

                // Outward-facing winding
                triangles[tri++] = current;
                triangles[tri++] = next;
                triangles[tri++] = current + 1;

                triangles[tri++] = current + 1;
                triangles[tri++] = next;
                triangles[tri++] = next + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    Material CreateMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.name = "MountainMaterial";
        mat.color = mountainColor;
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.2f);
        return mat;
    }
}
