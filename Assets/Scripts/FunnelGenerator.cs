using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Generates a procedural open-ended truncated cone (funnel) mesh with inward-facing normals.
/// Multiple intermediate rings ensure smooth collision on the curved inner wall.
/// The material is semi-transparent so you can see balls inside.
/// </summary>
public class FunnelGenerator : MonoBehaviour
{
    [Header("Funnel Dimensions")]
    public float topRadius = 5f;
    public float bottomRadius = 1f;
    public float height = 6f;

    [Header("Mesh Resolution")]
    public int segments = 32;   // around the circumference
    public int rings = 10;      // vertical subdivisions

    [Header("Appearance")]
    public Color funnelColor = new Color(0.6f, 0.6f, 0.65f, 0.35f); // semi-transparent grey

    public void Generate()
    {
        Mesh visualMesh = BuildFunnelMesh(inwardWinding: true);
        Mesh colliderMesh = BuildDoubleSidedFunnelMesh();

        // MeshFilter + MeshRenderer
        MeshFilter mf = gameObject.AddComponent<MeshFilter>();
        mf.mesh = visualMesh;

        MeshRenderer mr = gameObject.AddComponent<MeshRenderer>();
        mr.material = CreateTransparentMaterial();
        mr.shadowCastingMode = ShadowCastingMode.Off;

        // MeshCollider with double-sided mesh so balls collide
        // regardless of which side they approach from
        MeshCollider mc = gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = colliderMesh;
        mc.convex = false;
    }

    Mesh BuildFunnelMesh(bool inwardWinding)
    {
        Mesh mesh = new Mesh();
        mesh.name = inwardWinding ? "FunnelMeshInward" : "FunnelMeshOutward";

        int vertCount = (rings + 1) * (segments + 1);
        Vector3[] vertices = new Vector3[vertCount];
        Vector3[] normals  = new Vector3[vertCount];
        Vector2[] uvs      = new Vector2[vertCount];

        // Build vertex rings from bottom to top
        for (int ring = 0; ring <= rings; ring++)
        {
            float t = (float)ring / rings; // 0 = bottom, 1 = top
            float y = t * height;
            float radius = Mathf.Lerp(bottomRadius, topRadius, t);

            for (int seg = 0; seg <= segments; seg++)
            {
                float angle = (float)seg / segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;

                int idx = ring * (segments + 1) + seg;
                vertices[idx] = new Vector3(x, y, z);

                // Inward-facing normal (pointing toward center axis)
                // The cone surface normal has a vertical component due to the taper
                float slopeAngle = Mathf.Atan2(topRadius - bottomRadius, height);
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                // Rotate outward normal to account for cone slope, then invert
                Vector3 normal = -new Vector3(
                    outward.x * Mathf.Cos(slopeAngle),
                    Mathf.Sin(slopeAngle),
                    outward.z * Mathf.Cos(slopeAngle)
                ).normalized;
                normals[idx] = normal;

                uvs[idx] = new Vector2((float)seg / segments, t);
            }
        }

        // Build triangles (inward-facing winding)
        int triCount = rings * segments * 6;
        int[] triangles = new int[triCount];
        int tri = 0;

        for (int ring = 0; ring < rings; ring++)
        {
            for (int seg = 0; seg < segments; seg++)
            {
                int current = ring * (segments + 1) + seg;
                int next = current + (segments + 1);

                if (inwardWinding)
                {
                    // Inward-facing front face
                    triangles[tri++] = current;
                    triangles[tri++] = current + 1;
                    triangles[tri++] = next;

                    triangles[tri++] = current + 1;
                    triangles[tri++] = next + 1;
                    triangles[tri++] = next;
                }
                else
                {
                    // Outward-facing front face
                    triangles[tri++] = current;
                    triangles[tri++] = next;
                    triangles[tri++] = current + 1;

                    triangles[tri++] = current + 1;
                    triangles[tri++] = next;
                    triangles[tri++] = next + 1;
                }
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>
    /// Builds a double-sided funnel mesh for the collider.
    /// PhysX non-convex MeshColliders only collide on the front face of triangles,
    /// so we include both windings to ensure balls collide from the inside.
    /// </summary>
    Mesh BuildDoubleSidedFunnelMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "FunnelColliderMesh";

        int vertCount = (rings + 1) * (segments + 1);
        Vector3[] vertices = new Vector3[vertCount];

        for (int ring = 0; ring <= rings; ring++)
        {
            float t = (float)ring / rings;
            float y = t * height;
            float radius = Mathf.Lerp(bottomRadius, topRadius, t);

            for (int seg = 0; seg <= segments; seg++)
            {
                float angle = (float)seg / segments * Mathf.PI * 2f;
                int idx = ring * (segments + 1) + seg;
                vertices[idx] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    y,
                    Mathf.Sin(angle) * radius
                );
            }
        }

        // Double the triangles: one set for each winding direction
        int triCountPerSide = rings * segments * 6;
        int[] triangles = new int[triCountPerSide * 2];
        int tri = 0;

        for (int ring = 0; ring < rings; ring++)
        {
            for (int seg = 0; seg < segments; seg++)
            {
                int current = ring * (segments + 1) + seg;
                int next = current + (segments + 1);

                // Winding A
                triangles[tri++] = current;
                triangles[tri++] = current + 1;
                triangles[tri++] = next;

                triangles[tri++] = current + 1;
                triangles[tri++] = next + 1;
                triangles[tri++] = next;

                // Winding B (reversed)
                triangles[tri++] = current;
                triangles[tri++] = next;
                triangles[tri++] = current + 1;

                triangles[tri++] = current + 1;
                triangles[tri++] = next;
                triangles[tri++] = next + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    Material CreateTransparentMaterial()
    {
        // URP Lit shader with transparency
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.name = "FunnelMaterial";
        mat.color = funnelColor;

        // Enable transparency
        mat.SetFloat("_Surface", 1); // 0 = Opaque, 1 = Transparent
        mat.SetFloat("_Blend", 0);   // 0 = Alpha
        mat.SetFloat("_AlphaClip", 0);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0);
        mat.SetInt("_Cull", (int)CullMode.Off); // render both sides visually
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetShaderPassEnabled("ShadowCaster", false);

        return mat;
    }
}
