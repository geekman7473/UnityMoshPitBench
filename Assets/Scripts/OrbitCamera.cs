using UnityEngine;

/// <summary>
/// Orbits the camera around a focal point at constant rotational speed.
/// Camera height oscillates sinusoidally while horizontal orbit is constant angular velocity.
/// Camera always looks at the focal point.
/// </summary>
public class OrbitCamera : MonoBehaviour
{
    [Header("Orbit Settings")]
    public Vector3 focalPoint = new Vector3(0f, 5f, 0f);   // center of the funnel
    public float orbitRadius = 20f;                          // horizontal distance
    public float orbitSpeed = 15f;                           // degrees per second (constant)

    [Header("Height Oscillation")]
    public float baseHeight = 10f;                           // average camera height
    public float heightAmplitude = 5f;                       // sinusoidal height swing
    public float heightFrequency = 0.15f;                    // oscillations per second

    float currentAngle = 0f;

    void Update()
    {
        // Constant-speed rotation
        currentAngle += orbitSpeed * Time.deltaTime;

        // Sinusoidal height
        float height = baseHeight + Mathf.Sin(Time.time * heightFrequency * Mathf.PI * 2f) * heightAmplitude;

        // Compute camera position
        float rad = currentAngle * Mathf.Deg2Rad;
        Vector3 position = new Vector3(
            focalPoint.x + Mathf.Cos(rad) * orbitRadius,
            height,
            focalPoint.z + Mathf.Sin(rad) * orbitRadius
        );

        transform.position = position;
        transform.LookAt(focalPoint);
    }
}
