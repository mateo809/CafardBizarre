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
    private GameObject currentAlert;       

    private NavMeshAgent agent;
    private Transform target;
    private float lastSeenTime;
    private float lastAttackTime;
    private enum State { Patrol, Chase, Attack }
    private State state = State.Patrol;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (!agent)
            agent = gameObject.AddComponent<NavMeshAgent>();

        agent.speed = patrolSpeed;
        GoToRandomPoint();
    }

    void Update()
    {
        switch (state)
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

    // === ETAT PATROUILLE ===
    void Patrol()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            StartCoroutine(WaitAndMoveRandom());
        }
    }

    IEnumerator WaitAndMoveRandom()
    {
        state = State.Patrol;
        yield return new WaitForSeconds(idleTimeAtDestination);
        GoToRandomPoint();
    }

    void GoToRandomPoint()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += transform.position;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    // === DETECTION ===
    void DetectPlayer()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, viewDistance, playerLayer);
        bool playerVisible = false;

        foreach (var hit in hits)
        {
            Transform player = hit.transform;
            Vector3 dirToPlayer = (player.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dirToPlayer);

            if (angle < viewAngle * 0.5f)
            {
                float dist = Vector3.Distance(transform.position, player.position);
                if (!Physics.Raycast(transform.position + Vector3.up * 0.5f, dirToPlayer, dist, obstacleLayer))
                {
                    // joueur visible
                    target = player;
                    lastSeenTime = Time.time;
                    state = State.Chase;
                    agent.speed = chaseSpeed;
                    playerVisible = true;

                    if (currentAlert == null && alertPrefab != null && alertSpawnPoint != null)
                    {
                        currentAlert = Instantiate(alertPrefab, alertSpawnPoint.position, alertSpawnPoint.rotation, alertSpawnPoint);
                    }

                    break;
                }
            }
        }

        // Si le joueur a disparu depuis trop longtemps
        if (!playerVisible && target != null && Time.time - lastSeenTime > loseTargetTime)
        {
            target = null;
            agent.speed = patrolSpeed;
            state = State.Patrol;
            GoToRandomPoint();

            if (currentAlert != null)
            {
                Destroy(currentAlert);
                currentAlert = null;
            }
        }
    }

    // === ETAT CHASSE ===
    void Chase()
    {
        if (target == null)
        {
            if (Time.time - lastSeenTime > loseTargetTime)
            {
                target = null;
                state = State.Patrol;
                agent.speed = patrolSpeed;
                GoToRandomPoint();

                // supprime le feedback si présent
                if (currentAlert != null)
                {
                    Destroy(currentAlert);
                    currentAlert = null;
                }
            }
            return;
        }

        float dist = Vector3.Distance(transform.position, target.position);

        if (dist > viewDistance * 1.3f)
        {
            target = null;
            state = State.Patrol;
            agent.speed = patrolSpeed;
            GoToRandomPoint();

            if (currentAlert != null)
            {
                Destroy(currentAlert);
                currentAlert = null;
            }
            return;
        }

        if (dist <= attackRange)
        {
            state = State.Attack;
            agent.isStopped = true;
        }
        else
        {
            agent.isStopped = false;
            agent.SetDestination(target.position);
        }
    }

    // === ETAT ATTAQUE ===
    void Attack()
    {
        if (target == null)
        {
            state = State.Patrol;
            agent.isStopped = false;
            GoToRandomPoint();
            return;
        }

        transform.LookAt(target.position);
        float dist = Vector3.Distance(transform.position, target.position);

        if (dist > attackRange + 0.5f)
        {
            state = State.Chase;
            agent.isStopped = false;
            return;
        }

        if (Time.time - lastAttackTime > attackCooldown)
        {
            lastAttackTime = Time.time;
            Debug.Log($"{name} attaque {target.name} pour {attackDamage} dégâts !");
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
