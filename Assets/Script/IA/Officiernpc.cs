using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using PurrNet;
using BehaviorTree.Core;

namespace OfficeAI
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(NetworkAnimator))]
    public class OfficerNPC : NetworkBehaviour
    {
        [Header("Détection")]
        [SerializeField] private float sightRange = 10f;
        [SerializeField] private float sightAngle = 90f;
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private LayerMask obstacleLayer;

        [Header("Cafard")]
        [SerializeField] private Transform cockroachTarget;
        [SerializeField] private float cockroachDetectRange = 12f;
        [SerializeField] private float wallApproachDistance = 1.2f;
        [SerializeField] private float suctionRange = 2f;

        [Header("Vitesse")]
        [SerializeField] private float walkAgentSpeed = 2f;
        [SerializeField] private float runAgentSpeed = 3.2f;
        [SerializeField] private float searchAgentSpeed = 1.6f;

        [Header("Aspirateur")]
        [SerializeField] private float vacuumRange = 8f;
        [SerializeField] private float vacuumForce = 15f;
        [SerializeField] private float attackDuration = 2f;
        [SerializeField] private float attackCooldown = 1.5f;
        [SerializeField] private ParticleSystem vacuumFX;

        [Header("Objet aspirateur")]
        [SerializeField] private GameObject vacuumObject;
        [SerializeField] private Transform rightHand;
        [SerializeField] private Transform vacuumPickupPoint;
        [SerializeField] private Transform vacuumStorePoint;

        [Header("Patrouille")]
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private float waypointTolerance = 0.5f;

        [Header("Tâches bureau")]
        [SerializeField] private float minTaskDuration = 3f;
        [SerializeField] private float maxTaskDuration = 8f;
        [SerializeField] private float sitInDuration = 0.5f;
        [SerializeField] private float sitOutDuration = 0.4f;
        [SerializeField] private float pauseAfterWork = 3f;
        [SerializeField] private float deskStoppingDistance = 0.25f;
        [SerializeField] private float deskTurnSpeed = 6f;

        [Header("Recherche")]
        [SerializeField] private float searchDuration = 6f;
        [SerializeField] private float patrolSearchDuration = 4f;

        [Header("Animation")]
        [SerializeField] private Transform lookTarget;
        [SerializeField] private string m_HorizontalID = "Hor";
        [SerializeField] private string m_VerticalID = "Vert";
        [SerializeField] private string m_StateID = "State";
        [SerializeField] private string m_JumpID = "IsJump";
        [SerializeField] private string m_SitID = "Sit";
        [SerializeField] private string m_SprintID = "Sprint";
        [SerializeField] private string m_AttackID = "Attack";
        [SerializeField] private LookWeight m_LookWeight = new LookWeight(1f, 0.3f, 0.7f, 1f);
        [SerializeField] private float animFlow = 4.5f;

        public Transform assignedDesk;

        public SyncVar<NPCState> _state = new SyncVar<NPCState>(NPCState.Idle);
        public SyncVar<bool> _vacuumOn = new SyncVar<bool>(false);

        private NavMeshAgent _agent;
        private Animator _animator;
        private NetworkAnimator _netAnimator;
        private Blackboard _bb;
        private BTNode _tree;
        private Transform _playerTarget;

        private bool _isAttacking;
        private bool _hasVacuum;
        private bool _goingToVacuum;
        private bool _searchingLastSeen;
        private bool _patrollingSearch;
        private bool _returningVacuum;
        private bool _goingForCockroach;
        private bool _isSprinting;
        private bool _returningToLastSeen;
        private Vector3 _lastSeenCockroachPos;
        private bool _hasLastSeenCockroach;

        private Coroutine _workRoutine;
        private bool _postSitPause;
        private bool _isFacingDesk;
        private bool _isGoingToDesk;
        private bool _sitStarted;
        private bool _workingInterrupted;

        private bool _inPostWorkCooldown = false;
        private const float PostWorkCooldownSeconds = 2f;

        private float _attackEndTime;
        private float _nextAttackTime;
        private float _searchEndTime;
        private float _patrolSearchEndTime;

        private Vector3 _lastSeenPlayerPos;
        private bool _hasLastSeenPos;

        private Transform _vacuumOriginalParent;

        private float _flowState;
        private Vector2 _flowAxis;
        private Vector2 _targetAxis;
        private float _targetState;
        private bool _jumpState;

        private float _workStateEnteredTime;
        private const float WorkStateTimeoutSeconds = 30f;

        private static readonly int AttackHash = Animator.StringToHash("Attack");

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<Animator>();
            _netAnimator = GetComponent<NetworkAnimator>();
            _agent.speed = walkAgentSpeed;
            _agent.autoBraking = true;
            _agent.stoppingDistance = 0.1f;

            if (vacuumObject != null)
                _vacuumOriginalParent = vacuumObject.transform.parent;
        }

        protected override void OnSpawned()
        {
            if (!isServer) return;

            _bb = new Blackboard();
            _bb.Set(Blackboard.VacuumForce, vacuumForce);
            _bb.Set(Blackboard.AttackRange, vacuumRange);
            _bb.Set(Blackboard.PatrolPoints, patrolPoints);
            _bb.Set(Blackboard.PatrolIndex, 0);

            if (assignedDesk != null)
                _bb.Set(Blackboard.TargetPosition, assignedDesk.position);

            _tree = BuildTree();
        }

        private void Update()
        {
            if (isServer)
            {
                if (_tree != null && _bb != null)
                {
                    CheckWorkingTimeout();

                    if (_workRoutine == null && !_postSitPause && !_inPostWorkCooldown)
                    {
                        DetectPlayer();
                        DetectCockroach();
                        _tree.Tick();
                    }

                    UpdateAnimatorTargets(_state.value);
                }
            }

            UpdateAnimator();
        }

        private void CheckWorkingTimeout()
        {
            if (_state.value == NPCState.Working)
            {
                if (_workRoutine == null && !_postSitPause)
                {
                    ForceResetWorkState();
                    return;
                }

                if (_workRoutine != null && Time.time - _workStateEnteredTime > WorkStateTimeoutSeconds)
                {
                    StopCoroutine(_workRoutine);
                    _workRoutine = null;
                    ForceResetWorkState();
                }
            }
        }

        private void ForceResetWorkState()
        {
            _sitStarted = false;
            _isFacingDesk = false;
            _isGoingToDesk = false;
            _postSitPause = false;
            _workingInterrupted = false;
            _inPostWorkCooldown = false;

            if (_animator != null)
                _animator.SetBool(m_SitID, false);

            _agent.isStopped = false;
            _workRoutine = null;

            SetState(NPCState.Idle);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null || lookTarget == null) return;

            _animator.SetLookAtWeight(m_LookWeight.weight, m_LookWeight.body, m_LookWeight.head, m_LookWeight.eyes);
            _animator.SetLookAtPosition(lookTarget.position);
        }

        private void UpdateAnimator()
        {
            if (_animator == null) return;

            _animator.SetFloat(m_HorizontalID, _flowAxis.x);
            _animator.SetFloat(m_VerticalID, _flowAxis.y);
            _animator.SetFloat(m_StateID, Mathf.Clamp01(_flowState));
            _animator.SetBool(m_JumpID, _jumpState);
            _animator.SetBool(m_SitID, _sitStarted);
            _animator.SetBool(m_SprintID, _isSprinting);
            _animator.SetBool(m_AttackID, _isAttacking);

            if (_isAttacking || _workRoutine != null || _postSitPause || _isFacingDesk)
            {
                _targetAxis = Vector2.zero;
                _targetState = 1f;
                return;
            }

            _flowAxis = Vector2.ClampMagnitude(
                _flowAxis + animFlow * Time.deltaTime * (_targetAxis - _flowAxis).normalized,
                1f);

            _flowState = Mathf.Clamp01(
                _flowState + animFlow * Time.deltaTime * Mathf.Sign(_targetState - _flowState));
        }

        private void UpdateAnimatorTargets(NPCState state)
        {
            if (_isAttacking)
            {
                _targetAxis = Vector2.zero;
                _targetState = 1f;
                _jumpState = false;
                if (_playerTarget != null) lookTarget = _playerTarget;
                return;
            }

            if (_isFacingDesk || _workRoutine != null || _postSitPause || state == NPCState.Working)
            {
                _targetAxis = Vector2.zero;
                _targetState = 0.1f;
                _jumpState = false;
                _isSprinting = false;
                if (assignedDesk != null) lookTarget = assignedDesk;
                return;
            }

            if (state == NPCState.Attacking)
            {
                _targetAxis = Vector2.zero;
                _targetState = 1f;
                _jumpState = false;
                if (_playerTarget != null) lookTarget = _playerTarget;
                else if (cockroachTarget != null) lookTarget = cockroachTarget;
                return;
            }

            if (state == NPCState.Searching)
            {
                _targetAxis = new Vector2(0f, 1f);
                _targetState = 0.6f;
                _jumpState = false;
                if (_playerTarget != null) lookTarget = _playerTarget;
                else if (cockroachTarget != null) lookTarget = cockroachTarget;
                return;
            }

            if (state == NPCState.Returning)
            {
                _targetAxis = new Vector2(0f, 1f);
                _targetState = 0.7f;
                _jumpState = false;
                if (_playerTarget != null) lookTarget = _playerTarget;
                else if (cockroachTarget != null) lookTarget = cockroachTarget;
                return;
            }

            if (state == NPCState.Walking)
            {
                _targetAxis = new Vector2(0f, 1f);
                _targetState = 0.45f;
                _jumpState = false;
                if (_playerTarget != null) lookTarget = _playerTarget;
                else if (cockroachTarget != null) lookTarget = cockroachTarget;
                else if (assignedDesk != null) lookTarget = assignedDesk;
                return;
            }

            _targetAxis = Vector2.zero;
            _targetState = 0f;
            _jumpState = false;
        }

        private void SetWalk()
        {
            _isSprinting = false;
            _agent.speed = walkAgentSpeed;
        }

        private void SetRun()
        {
            _isSprinting = true;
            _agent.speed = runAgentSpeed;
        }

        private void SetSearchWalk()
        {
            _isSprinting = false;
            _agent.speed = searchAgentSpeed;
        }

        private void SetAttack(bool value)
        {
            _isAttacking = value;
            _agent.isStopped = value;
            if (value) _agent.ResetPath();

            if (_netAnimator != null)
                _netAnimator.SetBool(AttackHash, value);
            else if (_animator != null)
                _animator.SetBool(AttackHash, value);
        }

        private BTNode BuildTree()
        {
            return new Selector("Root", new List<BTNode>
            {
                new Sequence("AttackSequence", new List<BTNode>
                {
                    new ConditionNode("HasVacuum", HasVacuum),
                    new ConditionNode("PlayerVisible", IsPlayerVisible),
                    new ActionNode("FacePlayer", FacePlayer),
                    new ActionNode("VacuumAttack", VacuumAttack),
                }),

                new Selector("SearchRoutine", new List<BTNode>
                {
                    new Sequence("GetVacuum", new List<BTNode>
                    {
                        new ConditionNode("NeedVacuum", NeedVacuum),
                        new ActionNode("GoPickupVacuum", GoPickupVacuum),
                        new ActionNode("PickupVacuum", PickupVacuum),
                    }),

                    new Sequence("ReturnToLastSeenAfterPickup", new List<BTNode>
                    {
                        new ConditionNode("IsReturningToLastSeen", IsReturningToLastSeen),
                        new ActionNode("GoBackToLastSeen", GoBackToLastSeen),
                    }),

                    new Sequence("ReturnToLastSeen", new List<BTNode>
                    {
                        new ConditionNode("HasLastSeen", HasLastSeenPosition),
                        new ActionNode("GoToLastSeen", GoToLastSeen),
                        new ActionNode("SearchArea", SearchArea),
                    }),

                    new Sequence("PatrolSearch", new List<BTNode>
                    {
                        new ConditionNode("CanSearchPatrol", CanSearchPatrol),
                        new ActionNode("PatrolSearchAction", PatrolSearchAction),
                    }),

                    new Sequence("ReturnVacuum", new List<BTNode>
                    {
                        new ConditionNode("NeedReturnVacuum", NeedReturnVacuum),
                        new ActionNode("ReturnVacuumAction", ReturnVacuumAction),
                    }),
                }),

                new Selector("OfficeRoutine", new List<BTNode>
                {
                    new Sequence("WorkAtDesk", new List<BTNode>
                    {
                        new ConditionNode("HasDeskTarget", HasDeskTarget),
                        new ActionNode("MoveToDesk", MoveToDesk),
                        new ConditionNode("AtDesk", IsAtDesk),
                        new ActionNode("StartTask", StartTask),
                        new ActionNode("TickTask", TickTask),
                    }),

                    new ActionNode("Patrol", Patrol),
                }),
            });
        }

        private void DetectPlayer()
        {
            if (_workRoutine != null || _postSitPause || _isFacingDesk)
            {
                if (PlayerOrCockroachVisible())
                    InterruptWork();
                else
                    return;
            }

            _playerTarget = null;
            if (_bb != null) _bb.Set(Blackboard.IsPlayerVisible, false);

            var hits = Physics.OverlapSphere(transform.position, sightRange, playerLayer);
            if (hits.Length == 0)
            {
                if (_hasLastSeenPos && !_searchingLastSeen && !_patrollingSearch && !_returningToLastSeen && _workRoutine == null && !_postSitPause)
                    BeginSearch();
                return;
            }

            Vector3 eyePos = transform.position + Vector3.up * 1.5f;
            float halfAngle = sightAngle * 0.5f;

            Transform bestTarget = null;
            float bestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                Transform target = hit.transform.root;
                Vector3 targetPos = target.position + Vector3.up * 1.0f;
                Vector3 toTarget = targetPos - eyePos;
                float dist = toTarget.magnitude;
                if (dist < 0.001f) continue;

                float angle = Vector3.Angle(transform.forward, toTarget);
                if (angle > halfAngle) continue;

                Vector3 dir = toTarget / dist;
                bool blocked = Physics.Raycast(eyePos, dir, dist, obstacleLayer, QueryTriggerInteraction.Ignore);
                if (blocked) continue;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestTarget = target;
                }
            }

            if (bestTarget != null)
            {
                _playerTarget = bestTarget;
                _bb.Set(Blackboard.PlayerTransform, _playerTarget);
                _bb.Set(Blackboard.IsPlayerVisible, true);
                lookTarget = _playerTarget;
                SetRun();
                return;
            }

            if (_hasLastSeenPos && !_searchingLastSeen && !_patrollingSearch && !_returningToLastSeen && _workRoutine == null && !_postSitPause)
                BeginSearch();
        }

        private bool PlayerOrCockroachVisible()
        {
            if (_playerTarget != null) return true;
            return cockroachTarget != null;
        }

        private void InterruptWork()
        {
            if (_workRoutine != null)
            {
                StopCoroutine(_workRoutine);
                _workRoutine = null;
            }

            _workingInterrupted = true;
            _sitStarted = false;
            _isFacingDesk = false;
            _postSitPause = false;
            _inPostWorkCooldown = false;

            if (_animator != null)
                _animator.SetBool(m_SitID, false);

            _agent.isStopped = false;
            SetState(NPCState.Idle);

            if (_playerTarget != null)
            {
                if (TryGetReachablePointNear(_playerTarget.position, 2f, out var navPos))
                    _lastSeenPlayerPos = navPos;
                else
                    _lastSeenPlayerPos = _playerTarget.position;

                _hasLastSeenPos = true;
                _searchingLastSeen = true;
                SetRun();
            }
        }

        private void DetectCockroach()
        {
            if (_workRoutine != null || _postSitPause) return;
            if (cockroachTarget == null) return;
            if (_hasLastSeenPos || _goingToVacuum || _hasVacuum) return;

            float dist = Vector3.Distance(transform.position, cockroachTarget.position);
            if (dist > cockroachDetectRange) return;

            Vector3 toAgent = (transform.position - cockroachTarget.position).normalized;
            Vector3 wallPoint = cockroachTarget.position + toAgent * wallApproachDistance;

            if (TryGetReachablePointNear(wallPoint, suctionRange, out var navPos))
            {
                _lastSeenCockroachPos = navPos;
                _hasLastSeenCockroach = true;
                lookTarget = cockroachTarget;
                _goingForCockroach = true;
                _goingToVacuum = true;
                SetRun();
                _agent.isStopped = false;
            }
        }

        private bool TryGetReachablePointNear(Vector3 source, float maxDistance, out Vector3 result)
        {
            if (NavMesh.SamplePosition(source, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }

            result = source;
            return false;
        }

        private void BeginSearch()
        {
            if (_workRoutine != null || _postSitPause) return;

            if (!_hasVacuum)
            {
                _goingToVacuum = true;
                SetRun();
                _agent.isStopped = false;
                return;
            }

            _searchingLastSeen = true;
            _searchEndTime = Time.time + searchDuration;
            _agent.isStopped = false;
            SetSearchWalk();
            _agent.SetDestination(_lastSeenPlayerPos);
            _state.value = NPCState.Searching;
        }

        private bool IsPlayerVisible() => _bb.Get<bool>(Blackboard.IsPlayerVisible);
        private bool HasVacuum() => _hasVacuum;
        private bool NeedVacuum() => !_hasVacuum && _goingToVacuum;
        private bool IsReturningToLastSeen() => _hasVacuum && _returningToLastSeen;
        private bool HasLastSeenPosition() => _hasLastSeenPos && _hasVacuum && _searchingLastSeen;
        private bool CanSearchPatrol() => _hasVacuum && _patrollingSearch;
        private bool NeedReturnVacuum() => _hasVacuum && _returningVacuum;
        private bool HasDeskTarget() => assignedDesk != null && !_inPostWorkCooldown;

        private bool IsAtDestination()
        {
            if (_agent.pathPending) return false;
            if (!_agent.hasPath) return false;
            return _agent.remainingDistance <= Mathf.Max(waypointTolerance, _agent.stoppingDistance);
        }

        private bool IsAtDesk()
        {
            if (assignedDesk == null) return false;
            return IsAtTransform(assignedDesk.position);
        }

        private NodeStatus GoPickupVacuum()
        {
            if (vacuumPickupPoint == null) return NodeStatus.Failure;

            _agent.isStopped = false;
            SetRun();

            if (!_agent.hasPath || _agent.remainingDistance > waypointTolerance * 2f)
                _agent.SetDestination(vacuumPickupPoint.position);

            SetState(NPCState.Searching);

            if (IsAtTransform(vacuumPickupPoint.position))
                return NodeStatus.Success;

            return NodeStatus.Running;
        }

        private NodeStatus PickupVacuum()
        {
            if (vacuumObject == null || rightHand == null) return NodeStatus.Failure;

            vacuumObject.transform.SetParent(rightHand, false);
            vacuumObject.transform.localPosition = Vector3.zero;
            vacuumObject.transform.localRotation = Quaternion.identity;
            vacuumObject.transform.localScale = Vector3.one;

            _hasVacuum = true;
            _goingToVacuum = false;

            if (_hasLastSeenPos)
            {
                _returningToLastSeen = true;
                _agent.SetDestination(_lastSeenPlayerPos);
            }
            else if (_hasLastSeenCockroach)
            {
                _returningToLastSeen = true;
                _lastSeenPlayerPos = _lastSeenCockroachPos;
                _hasLastSeenPos = true;
                _agent.SetDestination(_lastSeenPlayerPos);
            }
            else
            {
                _patrollingSearch = true;
                _patrolSearchEndTime = Time.time + patrolSearchDuration;
            }

            _searchingLastSeen = false;
            SetRun();
            _agent.isStopped = false;
            SetState(NPCState.Returning);

            return NodeStatus.Success;
        }

        private NodeStatus GoBackToLastSeen()
        {
            if (!_hasLastSeenPos)
            {
                _returningToLastSeen = false;
                _patrollingSearch = true;
                _patrolSearchEndTime = Time.time + patrolSearchDuration;
                return NodeStatus.Failure;
            }

            _agent.isStopped = false;
            SetRun();
            _agent.SetDestination(_lastSeenPlayerPos);
            SetState(NPCState.Returning);

            if (IsAtTransform(_lastSeenPlayerPos))
            {
                _returningToLastSeen = false;
                _searchingLastSeen = true;
                _searchEndTime = Time.time + searchDuration;
                SetSearchWalk();
                SetState(NPCState.Searching);
                return NodeStatus.Success;
            }

            return NodeStatus.Running;
        }

        private NodeStatus GoToLastSeen()
        {
            if (!_hasLastSeenPos) return NodeStatus.Failure;

            _agent.isStopped = false;
            SetSearchWalk();
            _agent.SetDestination(_lastSeenPlayerPos);
            SetState(NPCState.Searching);

            if (IsAtTransform(_lastSeenPlayerPos))
                return NodeStatus.Success;

            return NodeStatus.Running;
        }

        private NodeStatus SearchArea()
        {
            if (_workRoutine != null || _postSitPause) return NodeStatus.Failure;

            _agent.isStopped = false;

            if (Time.time < _searchEndTime)
            {
                SetSearchWalk();
                SetState(NPCState.Searching);

                if (!_agent.hasPath || IsAtDestination())
                    _agent.SetDestination(_lastSeenPlayerPos);

                return NodeStatus.Running;
            }

            _searchingLastSeen = false;
            _patrollingSearch = true;
            _patrolSearchEndTime = Time.time + patrolSearchDuration;
            SetWalk();
            SetState(NPCState.Walking);
            return NodeStatus.Success;
        }

        private NodeStatus PatrolSearchAction()
        {
            if (_workRoutine != null || _postSitPause) return NodeStatus.Failure;
            if (patrolPoints == null || patrolPoints.Length == 0) return NodeStatus.Failure;

            if (Time.time >= _patrolSearchEndTime)
            {
                _returningVacuum = true;
                _patrollingSearch = false;
                return NodeStatus.Success;
            }

            int idx = _bb.Get<int>(Blackboard.PatrolIndex);
            if (!_agent.hasPath || IsAtDestination())
            {
                idx = (idx + 1) % patrolPoints.Length;
                _bb.Set(Blackboard.PatrolIndex, idx);
                _agent.SetDestination(patrolPoints[idx].position);
            }

            SetWalk();
            SetState(NPCState.Walking);
            return NodeStatus.Running;
        }

        private NodeStatus ReturnVacuumAction()
        {
            if (vacuumStorePoint == null || vacuumObject == null) return NodeStatus.Failure;

            _agent.isStopped = false;
            SetWalk();
            _agent.SetDestination(vacuumStorePoint.position);
            SetState(NPCState.Returning);

            if (IsAtTransform(vacuumStorePoint.position))
            {
                vacuumObject.transform.SetParent(_vacuumOriginalParent, false);
                vacuumObject.transform.position = vacuumStorePoint.position;
                vacuumObject.transform.rotation = vacuumStorePoint.rotation;

                _hasVacuum = false;
                _returningVacuum = false;
                _hasLastSeenPos = false;
                _goingToVacuum = false;
                _searchingLastSeen = false;
                _patrollingSearch = false;
                _goingForCockroach = false;
                _returningToLastSeen = false;
                _hasLastSeenCockroach = false;
                SetWalk();
                return NodeStatus.Success;
            }

            return NodeStatus.Running;
        }

        private NodeStatus FacePlayer()
        {
            if (_playerTarget == null) return NodeStatus.Failure;

            _agent.isStopped = true;
            _agent.ResetPath();
            SetRun();

            Vector3 dir = _playerTarget.position - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dir.normalized),
                    Time.deltaTime * 5f);
            }

            SetState(NPCState.Attacking);
            return NodeStatus.Success;
        }

        private NodeStatus VacuumAttack()
        {
            if (!_hasVacuum) return NodeStatus.Failure;
            if (_playerTarget == null) return NodeStatus.Failure;

            _agent.isStopped = true;
            _agent.ResetPath();

            if (Time.time < _nextAttackTime)
            {
                SetAttack(false);
                return NodeStatus.Failure;
            }

            float dist = Vector3.Distance(transform.position, _playerTarget.position);
            if (dist > vacuumRange)
            {
                StopAttack();
                _nextAttackTime = Time.time + attackCooldown;

                if (TryGetReachablePointNear(_playerTarget.position, 2f, out var navPos))
                    _lastSeenPlayerPos = navPos;
                else
                    _lastSeenPlayerPos = _playerTarget.position;

                _hasLastSeenPos = true;
                _searchingLastSeen = true;
                _agent.isStopped = false;
                SetRun();
                return NodeStatus.Failure;
            }

            if (!_isAttacking)
            {
                _attackEndTime = Time.time + attackDuration;
                _agent.isStopped = true;
                _agent.ResetPath();
                SetAttack(true);
                SetState(NPCState.Attacking);
            }

            Vector3 dirToNpc = (transform.position - _playerTarget.position).normalized;
            dirToNpc.y = 0f;

            if (_playerTarget.TryGetComponent<NetworkIdentity>(out var playerIdentity) && playerIdentity.owner.HasValue)
                ApplyVacuumForceToPlayer(playerIdentity.owner.Value, dirToNpc * vacuumForce);

            if (Time.time >= _attackEndTime)
            {
                StopAttack();
                _nextAttackTime = Time.time + attackCooldown;

                if (TryGetReachablePointNear(_playerTarget.position, 2f, out var navPos))
                    _lastSeenPlayerPos = navPos;
                else
                    _lastSeenPlayerPos = _playerTarget.position;

                _hasLastSeenPos = true;
                _searchingLastSeen = true;
                _agent.isStopped = false;
                SetRun();
                return NodeStatus.Success;
            }

            return NodeStatus.Running;
        }

        [TargetRpc]
        private void ApplyVacuumForceToPlayer(PlayerID target, Vector3 force)
        {
            if (_playerTarget == null) return;

            if (_playerTarget.TryGetComponent<PlayerVacuumReceiver>(out var receiver))
                receiver.ReceiveVacuumForce(force);
        }

        private void StopAttack()
        {
            _isAttacking = false;
            SetAttack(false);
            _agent.isStopped = false;
        }

        private void EndWork()
        {
            _sitStarted = false;
            _isFacingDesk = false;
            _isGoingToDesk = false;
            _postSitPause = false;
            _workingInterrupted = false;

            if (_animator != null)
                _animator.SetBool(m_SitID, false);

            _agent.isStopped = false;
            _workRoutine = null;

            SetState(NPCState.Idle);
            StartCoroutine(PostWorkCooldown());
        }

        private IEnumerator PostWorkCooldown()
        {
            _inPostWorkCooldown = true;

            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                int idx = _bb.Get<int>(Blackboard.PatrolIndex);
                idx = (idx + 1) % patrolPoints.Length;
                _bb.Set(Blackboard.PatrolIndex, idx);
                _agent.isStopped = false;
                SetWalk();
                _agent.SetDestination(patrolPoints[idx].position);
                SetState(NPCState.Walking);
            }

            yield return new WaitForSeconds(PostWorkCooldownSeconds);
            _inPostWorkCooldown = false;
        }

        private NodeStatus MoveToDesk()
        {
            if (assignedDesk == null) return NodeStatus.Failure;
            if (_workRoutine != null) return NodeStatus.Success;

            if (_workingInterrupted)
            {
                _workingInterrupted = false;
                _isFacingDesk = false;
            }

            _agent.isStopped = false;
            _agent.stoppingDistance = deskStoppingDistance;
            SetWalk();
            _agent.SetDestination(assignedDesk.position);
            SetState(NPCState.Walking);
            _isGoingToDesk = true;

            if (IsAtDesk())
            {
                _agent.isStopped = true;
                _agent.ResetPath();
                _isGoingToDesk = false;
                _isFacingDesk = true;
                return NodeStatus.Success;
            }

            return NodeStatus.Running;
        }

        private NodeStatus StartTask()
        {
            if (_workRoutine != null) return NodeStatus.Success;
            if (_postSitPause) return NodeStatus.Success;
            if (assignedDesk == null) return NodeStatus.Failure;
            if (!IsAtDesk()) return NodeStatus.Running;
            if (!_isFacingDesk) return NodeStatus.Running;

            _workRoutine = StartCoroutine(WorkSequence());
            return NodeStatus.Success;
        }

        private IEnumerator WorkSequence()
        {
            _agent.isStopped = true;
            _agent.ResetPath();
            _isSprinting = false;
            _sitStarted = false;

            _workStateEnteredTime = Time.time;

            Quaternion targetRot = assignedDesk.rotation;
            targetRot = Quaternion.Euler(0f, targetRot.eulerAngles.y, 0f);

            while (Quaternion.Angle(transform.rotation, targetRot) > 1f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * deskTurnSpeed);
                yield return null;
            }

            _sitStarted = true;
            SetState(NPCState.Working);

            yield return new WaitForSeconds(sitInDuration);

            float workTime = Random.Range(minTaskDuration, maxTaskDuration);
            float timer = 0f;

            while (timer < workTime)
            {
                if (_playerTarget != null || cockroachTarget != null)
                {
                    InterruptWork();
                    yield break;
                }

                timer += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(sitOutDuration);

            _sitStarted = false;
            if (_animator != null)
                _animator.SetBool(m_SitID, false);

            _postSitPause = true;
            yield return new WaitForSeconds(pauseAfterWork);
            _postSitPause = false;

            EndWork();
        }

        private NodeStatus TickTask() => _workRoutine != null ? NodeStatus.Running : NodeStatus.Success;

        private NodeStatus Patrol()
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
                return NodeStatus.Failure;

            if (_workRoutine != null || _postSitPause || _isFacingDesk)
                return NodeStatus.Running;

            int idx = _bb.Get<int>(Blackboard.PatrolIndex);

            if (!_agent.hasPath || IsAtDestination())
            {
                idx = (idx + 1) % patrolPoints.Length;
                _bb.Set(Blackboard.PatrolIndex, idx);
                _agent.SetDestination(patrolPoints[idx].position);
            }

            SetWalk();
            SetState(NPCState.Walking);
            return NodeStatus.Running;
        }

        private bool IsAtTransform(Vector3 targetPos)
        {
            if (_agent.pathPending) return false;
            if (!_agent.hasPath) return Vector3.Distance(transform.position, targetPos) <= waypointTolerance;
            return _agent.remainingDistance <= Mathf.Max(waypointTolerance, _agent.stoppingDistance);
        }

        private void SetState(NPCState newState)
        {
            _state.value = newState;
            _vacuumOn.value = newState == NPCState.Attacking;
            UpdateAnimatorTargets(newState);
        }

        private void OnVacuumChanged(bool isOn)
        {
            if (vacuumFX == null) return;
            if (isOn) vacuumFX.Play();
            else vacuumFX.Stop();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, sightRange);

            float half = sightAngle * 0.5f * Mathf.Deg2Rad;
            var left = new Vector3(Mathf.Sin(-half), 0, Mathf.Cos(-half));
            var right = new Vector3(Mathf.Sin(half), 0, Mathf.Cos(half));
            Gizmos.DrawRay(transform.position, transform.TransformDirection(left) * sightRange);
            Gizmos.DrawRay(transform.position, transform.TransformDirection(right) * sightRange);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, vacuumRange);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, cockroachDetectRange);
        }
#endif

        [System.Serializable]
        private struct LookWeight
        {
            public float weight;
            public float body;
            public float head;
            public float eyes;

            public LookWeight(float weight, float body, float head, float eyes)
            {
                this.weight = weight;
                this.body = body;
                this.head = head;
                this.eyes = eyes;
            }
        }
    }

    public enum NPCState { Idle, Walking, Working, Attacking, Searching, Returning }
}