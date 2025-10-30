using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    [Header("Navigation")]
    public float patrolRadius = 15f;
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;

    [Header("Vision")]
    public float viewDistance = 12f;
    [Range(0, 360)] public float viewAngle = 120f;
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;
    public GameObject FovTransform;

    [Header("Combat")]
    public float attackRange = 1.5f;
    public float attackCooldown = 1.2f;
    public int attackDamage = 10;

    [Header("Comportement")]
    public float loseTargetTime = 3f;
    public float idleTimeAtDestination = 2f;

    [Header("Feedback Visuel")]
    public GameObject alertPrefab;
    public Transform alertSpawnPoint;

    private GameObject _currentAlert;
    private NavMeshAgent _agent;
    private Transform _target;
    private float _lastSeenTime;
    private float _lastAttackTime;

    private enum State { Patrol, Chase, Attack }
    private State _state = State.Patrol;

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (!_agent)
            _agent = gameObject.AddComponent<NavMeshAgent>();

        _agent.speed = patrolSpeed;
        GoToRandomPoint();
    }

    void Update()
    {
        switch (_state)
        {
            case State.Patrol:
                Patrol();
                DetectPlayer();
                break;
            case State.Chase:
                Chase();
                DetectPlayer();
                break;
            case State.Attack:
                Attack();
                break;
        }
    }

    void Patrol()
    {
        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
        {
            StartCoroutine(WaitAndMoveRandom());
        }
    }

    IEnumerator WaitAndMoveRandom()
    {
        _state = State.Patrol;
        yield return new WaitForSeconds(idleTimeAtDestination);
        GoToRandomPoint();
    }

    void GoToRandomPoint()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += transform.position;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
        {
            _agent.SetDestination(hit.position);
        }
    }

    void DetectPlayer()
    {
        Vector3 eyePos = FovTransform ? FovTransform.transform.position : transform.position;

        Collider[] hits = Physics.OverlapSphere(eyePos, viewDistance, playerLayer);
        bool playerVisible = false;

        foreach (var hit in hits)
        {
            Transform player = hit.transform;
            Vector3 dirToPlayer = (player.position - eyePos).normalized;
            float angle = Vector3.Angle(FovTransform ? FovTransform.transform.forward : transform.forward, dirToPlayer);

            if (angle < viewAngle * 0.5f)
            {
                float dist = Vector3.Distance(eyePos, player.position);
                if (!Physics.Raycast(eyePos, dirToPlayer, dist, obstacleLayer))
                {
                    _target = player;
                    _lastSeenTime = Time.time;
                    _state = State.Chase;
                    _agent.speed = chaseSpeed;
                    playerVisible = true;

                    if (_currentAlert == null && alertPrefab != null && alertSpawnPoint != null)
                    {
                        _currentAlert = Instantiate(alertPrefab, alertSpawnPoint.position, alertSpawnPoint.rotation, alertSpawnPoint);
                    }

                    break;
                }
            }
        }

        if (!playerVisible && _target != null && Time.time - _lastSeenTime > loseTargetTime)
        {
            _target = null;
            _agent.speed = patrolSpeed;
            _state = State.Patrol;
            GoToRandomPoint();

            if (_currentAlert != null)
            {
                Destroy(_currentAlert);
                _currentAlert = null;
            }
        }
    }

    void Chase()
    {
        if (_target == null)
        {
            if (Time.time - _lastSeenTime > loseTargetTime)
            {
                _target = null;
                _state = State.Patrol;
                _agent.speed = patrolSpeed;
                GoToRandomPoint();

                if (_currentAlert != null)
                {
                    Destroy(_currentAlert);
                    _currentAlert = null;
                }
            }
            return;
        }

        float dist = Vector3.Distance(transform.position, _target.position);

        if (dist > viewDistance * 1.3f)
        {
            _target = null;
            _state = State.Patrol;
            _agent.speed = patrolSpeed;
            GoToRandomPoint();

            if (_currentAlert != null)
            {
                Destroy(_currentAlert);
                _currentAlert = null;
            }
            return;
        }

        if (dist <= attackRange)
        {
            _state = State.Attack;
            _agent.isStopped = true;
        }
        else
        {
            _agent.isStopped = false;
            _agent.SetDestination(_target.position);
        }
    }

    void Attack()
    {
        if (_target == null)
        {
            _state = State.Patrol;
            _agent.isStopped = false;
            GoToRandomPoint();
            return;
        }

        transform.LookAt(_target.position);
        float dist = Vector3.Distance(transform.position, _target.position);

        if (dist > attackRange + 0.5f)
        {
            _state = State.Chase;
            _agent.isStopped = false;
            return;
        }

        if (Time.time - _lastAttackTime > attackCooldown)
        {
            _lastAttackTime = Time.time;

            PlayerHealth playerHealth = _target.GetComponent<PlayerHealth>();
            if (playerHealth != null && playerHealth.isOwner)
            {
                playerHealth.TakeDamage(30);
            }

            Debug.Log($"{name} attaque {_target.name} pour 30 dégâts !");
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, patrolRadius);

        Vector3 left = Quaternion.Euler(0, -viewAngle / 2, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, viewAngle / 2, 0) * transform.forward;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + left * viewDistance);
        Gizmos.DrawLine(transform.position, transform.position + right * viewDistance);
    }
}
