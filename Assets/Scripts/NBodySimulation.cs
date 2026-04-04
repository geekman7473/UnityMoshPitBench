using UnityEngine;

/// <summary>
/// N-body gravitational simulation. Computes pairwise gravitational attraction
/// between all Rigidbodies and applies forces each FixedUpdate.
/// Uses O(n²/2) symmetric force computation with softening to prevent singularities.
/// </summary>
public class NBodySimulation : MonoBehaviour
{
    [Header("Gravity Settings")]
    public float gravitationalConstant = 0.5f;
    public float softening = 2f;

    private Rigidbody[] _bodies;
    private Vector3[] _positions;
    private float[] _masses;
    private Vector3[] _forces;

    void Start()
    {
        CacheBodies();
    }

    public void CacheBodies()
    {
        _bodies = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
        int n = _bodies.Length;
        _positions = new Vector3[n];
        _masses = new float[n];
        _forces = new Vector3[n];
    }

    void FixedUpdate()
    {
        if (_bodies == null || _bodies.Length == 0) return;

        int n = _bodies.Length;
        float softeningSqr = softening * softening;

        for (int i = 0; i < n; i++)
        {
            if (_bodies[i] == null) continue;
            _positions[i] = _bodies[i].position;
            _masses[i] = _bodies[i].mass;
            _forces[i] = Vector3.zero;
        }

        for (int i = 0; i < n; i++)
        {
            if (_bodies[i] == null) continue;
            for (int j = i + 1; j < n; j++)
            {
                if (_bodies[j] == null) continue;

                Vector3 diff = _positions[j] - _positions[i];
                float distSqr = diff.sqrMagnitude + softeningSqr;
                float dist = Mathf.Sqrt(distSqr);
                float forceMag = gravitationalConstant * _masses[i] * _masses[j] / (distSqr * dist);
                Vector3 force = diff * forceMag;

                _forces[i] += force;
                _forces[j] -= force;
            }
        }

        for (int i = 0; i < n; i++)
        {
            if (_bodies[i] != null)
                _bodies[i].AddForce(_forces[i]);
        }
    }
}
