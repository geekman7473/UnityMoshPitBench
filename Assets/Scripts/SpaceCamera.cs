using UnityEngine;

/// <summary>
/// Camera controller for the space scene. Computes center of mass of all
/// Rigidbodies each frame, orbits at a fixed distance, always looking at COM.
/// </summary>
public class SpaceCamera : MonoBehaviour
{
    [Header("Settings")]
    public float distance = 120f;          // fixed distance from center of mass
    public float smoothSpeed = 5f;         // smoothing for target tracking
    public float orbitSpeed = 5f;          // slow constant orbit (degrees/sec)

    private Rigidbody[] _bodies;
    private float _currentAngle;
    private Vector3 _smoothTarget;

    void Start()
    {
        _bodies = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
        _smoothTarget = Vector3.zero;
    }

    void LateUpdate()
    {
        if (_bodies == null || _bodies.Length == 0) return;

        // Compute center of mass
        Vector3 com = Vector3.zero;
        float totalMass = 0f;

        for (int i = 0; i < _bodies.Length; i++)
        {
            if (_bodies[i] == null) continue;
            float m = _bodies[i].mass;
            com += _bodies[i].position * m;
            totalMass += m;
        }

        if (totalMass < 0.001f) return;
        com /= totalMass;

        // Smooth target tracking
        _smoothTarget = Vector3.Lerp(_smoothTarget, com, Time.deltaTime * smoothSpeed);

        // Slow orbit around the center of mass at fixed distance
        _currentAngle += orbitSpeed * Time.deltaTime;
        float rad = _currentAngle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Cos(rad) * distance,
            Mathf.Sin(rad * 0.3f) * distance * 0.25f,
            Mathf.Sin(rad) * distance
        );

        transform.position = _smoothTarget + offset;
        transform.LookAt(_smoothTarget);
    }
}
