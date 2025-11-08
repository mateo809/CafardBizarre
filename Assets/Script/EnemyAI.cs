using PurrNet;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : NetworkBehaviour
{
    [Header("Navigation")]
    public float patrolRadius = 15f;
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;
    public float chaseAcceleration = 5f;

    [Header("Vision")]
    public float viewDistance = 12f;
    [Range(0, 360)] public float viewAngle = 120f;
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;
    public GameObject FovTransform;

    [Header("Spotlight")]
    public Light spotlight;
    public float spotlightDetectionAngle = 30f;
    private NetworkTransform _spotlightNetworkTransform;

    [Header("Combat")]
    public float attackRange = 3f;
    public float attackCooldown = 2f;
    public int attackDamage = 10;
    public float attackDistance = 5f;

    [Header("Comportement")]
    public float loseTargetTime = 3f;
    public float idleTimeAtDestination = 2f;
    public float minPatrolWaitTime = 1f;
    public float maxPatrolWaitTime = 3f;

    [Header("Feedback Visuel")]
    public GameObject alertPrefab;
    public Transform alertSpawnPoint;

    [Header("Animation")]
    [SerializeField] private NetworkAnimator _animator;
    public bool useRootMotion = false;

    [Header("VFX")]
    [SerializeField] private GameObject _attackEffect;

    private GameObject _currentAlert;
    private NavMeshAgent _agent;
    private Transform _target;
    private float _lastSeenTime;
    private float _lastAttackTime;
    private PlayerStress _playerStress;
    private bool _isAttacking;
    private bool _patrolCoroutineRunning = false;

    private enum State { Patrol, Chase, Attack }
    private State _state = State.Patrol;

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>() ?? gameObject.AddComponent<NavMeshAgent>();
        _animator = _animator ?? GetComponentInChildren<NetworkAnimator>();

        _animator.applyRootMotion = useRootMotion;
        _agent.updatePosition = !useRootMotion;
        _agent.updateRotation = !useRootMotion;

        _agent.speed = patrolSpeed;
        _agent.stoppingDistance = 0.5f;

        if (spotlight != null)
        {
            _spotlightNetworkTransform = spotlight.GetComponent<NetworkTransform>();
        }

        GoToRandomPoint();
        SetAnimationState("Walk");
    }

    void Update()
    {
        if (!isServer) return;

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

        UpdateSpotlight();

        if (useRootMotion) OnAnimatorMove();
    }

    void Patrol()
    {
        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance && !_patrolCoroutineRunning)
            StartCoroutine(WaitAndMoveRandom());
    }

    IEnumerator WaitAndMoveRandom()
    {
        _patrolCoroutineRunning = true;
        float randomWait = Random.Range(minPatrolWaitTime, maxPatrolWaitTime);
        yield return new WaitForSeconds(randomWait);
        GoToRandomPoint();
        SetAnimationState("Walk");
        _patrolCoroutineRunning = false;
    }

    void GoToRandomPoint()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius + transform.position;
        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
        {
            _agent.speed = patrolSpeed;
            _agent.SetDestination(hit.position);
            SetAnimationState("Walk");
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
                    SetAnimationState("Run");
                    playerVisible = true;

                    if (_playerStress == null)
                        _playerStress = _target.GetComponent<PlayerStress>();

                    if (_playerStress != null)
                        _playerStress.SetDetected(true);

                    if (_currentAlert == null && alertPrefab && alertSpawnPoint)
                        _currentAlert = Instantiate(alertPrefab, alertSpawnPoint.position, alertSpawnPoint.rotation, alertSpawnPoint);
                    break;
                }
            }
        }

        if (!playerVisible && _target != null)
        {
            float timeSinceLastSeen = Time.time - _lastSeenTime;

            if (timeSinceLastSeen > loseTargetTime)
            {
                GoBackToPatrol();
            }
            else if (_playerStress != null)
            {
                _playerStress.SetDetected(false);
            }
        }
        else if (!playerVisible && _playerStress != null)
        {
            _playerStress.SetDetected(false);
        }
    }

    void Chase()
    {
        if (_target == null)
        {
            GoBackToPatrol();
            return;
        }

        float dist = Vector3.Distance(transform.position, _target.position);

        if (Time.time - _lastSeenTime > loseTargetTime)
        {
            GoBackToPatrol();
            return;
        }

        if (dist <= attackRange)
        {
            _state = State.Attack;
            _agent.isStopped = true;
            SetAnimationState("Attack");
        }
        else
        {
            _agent.isStopped = false;
            _agent.speed = chaseSpeed;
            _agent.acceleration = chaseAcceleration;
            _agent.SetDestination(_target.position);
            SetAnimationState("Run");
            FaceTarget(_target.position);
        }
    }

    void Attack()
    {
        if (_target == null)
        {
            GoBackToPatrol();
            return;
        }

        float dist = Vector3.Distance(transform.position, _target.position);

        bool playerFar = dist > attackRange + 2f;
        bool lostForAWhile = Time.time - _lastSeenTime > loseTargetTime;

        if (playerFar && lostForAWhile)
        {
            GoBackToPatrol();
            return;
        }

        if (dist > attackRange + 1f)
        {
            _state = State.Chase;
            _agent.isStopped = false;
            return;
        }

        FaceTarget(_target.position);

        if (!_isAttacking && Time.time >= _lastAttackTime + attackCooldown)
        {
            StartCoroutine(DoAttack());
        }
    }

    IEnumerator DoAttack()
    {
        _isAttacking = true;
        _lastAttackTime = Time.time;

        SetAnimationState("Attack");

        float attackDelay = attackCooldown * 0.5f;
        yield return new WaitForSeconds(attackDelay);

        ApplyAttackDamageRPC();

        yield return new WaitForSeconds(attackCooldown * 0.5f);

        SetAnimationState("Idle");
        _isAttacking = false;
    }

    [ServerRpc]
    private void ApplyAttackDamageRPC()
    {
        if (_target != null)
        {
            PlayerHealth playerHealth = _target.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage);
                Debug.Log($"{name} attaque {_target.name} pour {attackDamage} dégâts !");
            }
        }
    }

    void GoBackToPatrol()
    {
        _state = State.Patrol;
        _agent.isStopped = false;
        _agent.speed = patrolSpeed;
        _target = null;
        _playerStress = null;
        if (_currentAlert) Destroy(_currentAlert);
        GoToRandomPoint();
        SetAnimationState("Walk");
    }

    void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 5f);
    }

    void SetAnimationState(string state)
    {
        if (_animator == null) return;

        SyncAnimationStateRPC(state);
    }

    [ObserversRpc]
    private void SyncAnimationStateRPC(string state)
    {
        if (_animator == null) return;

        _animator.SetBool("Walk", false);
        _animator.SetBool("Run", false);
        _animator.SetBool("Attack", false);

        switch (state)
        {
            case "Walk":
                _animator.SetBool("Walk", true);
                break;
            case "Run":
                _animator.SetBool("Run", true);
                break;
            case "Attack":
                _animator.SetBool("Attack", true);
                break;
            case "Idle":
                // Idle = tous les bools à false
                break;
        }
    }

    void OnAnimatorMove()
    {
        if (useRootMotion && _agent && _animator)
        {
            _agent.velocity = _animator.deltaPosition / Time.deltaTime;
            transform.rotation = _animator.rootRotation;
        }
    }

    void UpdateSpotlight()
    {
        if (!spotlight || !isServer) return;

        Vector3 targetPos = Vector3.zero;
        bool hasTarget = false;

        if (_target != null)
        {
            targetPos = _target.position;
            hasTarget = true;
        }
        else if (FovTransform)
        {
            targetPos = FovTransform.transform.position + FovTransform.transform.forward * viewDistance;
            hasTarget = true;
        }

        if (hasTarget)
        {
            Vector3 dirToTarget = (targetPos - spotlight.transform.position).normalized;
            Quaternion targetRot = Quaternion.LookRotation(dirToTarget);
            spotlight.transform.rotation = Quaternion.Slerp(
                spotlight.transform.rotation,
                targetRot,
                Time.deltaTime * 5f
            );

            SyncSpotlightRotationRPC(spotlight.transform.rotation);
        }
    }

    [ObserversRpc]
    private void SyncSpotlightRotationRPC(Quaternion newRotation)
    {
        if (spotlight != null)
        {
            spotlight.transform.rotation = newRotation;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!FovTransform) return;

        Vector3 pos = FovTransform.transform.position;
        Vector3 forward = FovTransform.transform.forward;

        Gizmos.color = new Color(1, 1, 0, 0.2f);
        int segments = 30;
        for (int i = 0; i <= segments; i++)
        {
            float angle = -viewAngle / 2 + viewAngle * i / (float)segments;
            Quaternion rot = Quaternion.AngleAxis(angle, FovTransform.transform.up);
            Vector3 dir = rot * forward;
            Gizmos.DrawLine(pos, pos + dir * viewDistance);
        }
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pos, viewDistance);
    }
}