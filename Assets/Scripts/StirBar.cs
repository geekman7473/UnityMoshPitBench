using UnityEngine;

/// <summary>
/// A kinematic spinning bar positioned inside the funnel near the bottom opening.
/// Collides with balls and pushes them around like a magnetic stir bar.
/// </summary>
public class StirBar : MonoBehaviour
{
    [Header("Spin Settings")]
    public float spinSpeed = 180f; // degrees per second

    [Header("Bar Dimensions")]
    public float barLength = 1.8f; // length of the bar (should be slightly less than funnel bottom diameter)
    public float barWidth = 0.15f;
    public float barHeight = 0.15f;

    [Header("Appearance")]
    public Color barColor = new Color(0.8f, 0.8f, 0.85f, 1f); // light metallic

    public void Generate()
    {
        // Create the visual bar from a cube primitive
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = "StirBarVisual";
        bar.transform.SetParent(transform, false);
        bar.transform.localScale = new Vector3(barLength, barHeight, barWidth);
        bar.transform.localPosition = Vector3.zero;

        // Set material
        Renderer rend = bar.GetComponent<Renderer>();
        Material mat = CreateMaterial();
        rend.material = mat;

        // The cube already has a BoxCollider from CreatePrimitive — keep it.

        // Add a kinematic Rigidbody to the root so physics engine
        // properly handles collision response with dynamic balls
        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void FixedUpdate()
    {
        // Rotate via MoveRotation for proper kinematic collision detection
        Quaternion deltaRotation = Quaternion.Euler(0f, spinSpeed * Time.fixedDeltaTime, 0f);
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.MoveRotation(rb.rotation * deltaRotation);
        }
    }

    Material CreateMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.name = "StirBarMaterial";
        mat.color = barColor;
        mat.SetFloat("_Metallic", 0.7f);
        mat.SetFloat("_Smoothness", 0.8f);
        return mat;
    }
}
