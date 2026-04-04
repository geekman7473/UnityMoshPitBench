using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controls an individual maze agent: plays/stops running animation based on
/// actual movement speed. Lets NavMeshAgent handle all pathfinding naturally.
/// Only intervenes if the agent has no path at all.
/// </summary>
public class AgentController : MonoBehaviour
{
    private NavMeshAgent _agent;
    private Animation _anim;
    private Vector3 _destination;
    private float _repathTimer;
    private static int _agentSeedCounter = 0;

    void Start()
    {
        System.Random rng = new System.Random(12345 + _agentSeedCounter++);
        _agent = GetComponent<NavMeshAgent>();
        _anim = GetComponent<Animation>();

        if (_agent != null)
        {
            _destination = _agent.destination;
            // Stagger so not all agents repath on the same frame
            _repathTimer = (float)rng.NextDouble() * 5f;
        }
    }

    void Update()
    {
        if (_agent == null || !_agent.isOnNavMesh) return;

        // Animate based on actual velocity
        float speed = _agent.velocity.magnitude;
        if (_anim != null)
        {
            if (speed > 0.1f)
            {
                if (!_anim.isPlaying)
                    _anim.Play("Run");
                _anim["Run"].speed = Mathf.Clamp(speed / _agent.speed, 0.3f, 2f);
            }
            else
            {
                _anim.Stop();
            }
        }

        // Only re-request destination occasionally, and only if agent has no path
        _repathTimer += Time.deltaTime;
        if (_repathTimer >= 5f)
        {
            _repathTimer = 0f;
            if (!_agent.hasPath || _agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                _agent.SetDestination(_destination);
            }
        }
    }
}
