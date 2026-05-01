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
        [Header("Audio")]
        [SerializeField] private float stepInterval = 0.5f;
        private float stepTimer;

        [Header("Détection")]
        [SerializeField] private float sightRange = 10f;
        [SerializeField] private float sightAngle = 180f;
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private LayerMask obstacleLayer;
        [SerializeField] private bool debugDetection = true;

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
        [SerializeField] private float pointWaitDuration = 1.5f;

        [Header("Cycle Travail / Patrouille")]
        [SerializeField] private float workPhaseDuration = 30f;
        [SerializeField] private float patrolPhaseDuration = 20f;

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
        [SerializeField] private float searchWaitDuration = 1f;

        [Header("Animation")]
        [SerializeField] private Transform lookTarget;
        [SerializeField] private string m_HorizontalID = "Hor";
        [SerializeField] private string m_VerticalID = "Vert";
        [SerializeField] private string m_StateID = "State";
        [SerializeField] private string m_JumpID = "IsJump";
        [SerializeField] private string m_SitID = "Sit";
        [SerializeField] private string m_SprintID = "Sprint";
        [SerializeField] private string m_AttackID = "Attack";
        [SerializeField] private string m_IdleRelaxedID = "Idle_Relaxed";
        [SerializeField] private string m_IdleLookAroundID = "Idle_Look_Around";
        [SerializeField] private string m_IdleID = "Idle";
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
        private Transform _currentTarget;

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

        private bool _inWorkPhase = true;
        private float _phaseEndTime;
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

        private float _waitUntilTime;
        private bool _waitingAtPoint;
        private int _currentPatrolIndex = -1;
        private int _currentSearchPatrolIndex = -1;
        private bool _pathSet;

        public SyncVar<float> netHorizontal = new SyncVar<float>();
        public SyncVar<float> netVertical = new SyncVar<float>();
        public SyncVar<float> netStateFloat = new SyncVar<float>();

        private int _hashHorizontal;
        private int _hashVertical;
        private int _hashState;
        private int _hashJump;
        private int _hashSit;
        private int _hashSprint;
        private int _hashAttack;
        private int _hashIdleRelaxed;
        private int _hashIdleLookAround;

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

            // Précalcul des hashes une seule fois
            _hashHorizontal = Animator.StringToHash(m_HorizontalID);
            _hashVertical = Animator.StringToHash(m_VerticalID);
            _hashState = Animator.StringToHash(m_StateID);
            _hashJump = Animator.StringToHash(m_JumpID);
            _hashSit = Animator.StringToHash(m_SitID);
            _hashSprint = Animator.StringToHash(m_SprintID);
            _hashAttack = Animator.StringToHash(m_AttackID);
            _hashIdleRelaxed = Animator.StringToHash(m_IdleRelaxedID);
            _hashIdleLookAround = Animator.StringToHash(m_IdleLookAroundID);
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

            _inWorkPhase = true;
            _phaseEndTime = Time.time + workPhaseDuration;
            _tree = BuildTree();
        }

        private void Update()
        {
            if (isServer && _tree != null && _bb != null)
            {
                HandleFootsteps();
                CheckWorkingTimeout();
                UpdateWorkCycle();
                DetectPlayer();
                DetectCockroach();

                if (_workRoutine == null && !_postSitPause)
                    _tree.Tick();

                UpdateAnimatorTargets(_state.value);
            }

            UpdateAnimator();
        }
        private void HandleFootsteps()
        {
            if (!_agent.enabled) return;

            bool isMoving =
                !_agent.isStopped &&
                _agent.hasPath &&
                _agent.velocity.sqrMagnitude > 0.1f;

            if (!isMoving) return;

            stepTimer -= Time.deltaTime;

            if (stepTimer <= 0f)
            {
                stepTimer = stepInterval;

                PlayStepObserversRpc(transform.position);
            }
        }

        [ObserversRpc]
        private void PlayStepObserversRpc(Vector3 pos)
        {
            if (AudioController.Instance == null) return;

            AudioController.Instance.PlayLoopedSound(AudioType.mobStep, AudioSourceType.Mob);
        }

        private void UpdateWorkCycle()
        {
            if (_hasLastSeenPos || _goingToVacuum || _hasVacuum) return;

            if (Time.time >= _phaseEndTime)
            {
                _inWorkPhase = !_inWorkPhase;
                _phaseEndTime = Time.time + (_inWorkPhase ? workPhaseDuration : patrolPhaseDuration);

                if (!_inWorkPhase)
                {
                    if (_workRoutine != null)
                    {
                        StopCoroutine(_workRoutine);
                        _workRoutine = null;
                        _sitStarted = false;
                        _isFacingDesk = false;
                        _postSitPause = false;
                        SetNetBool(_hashSit, false);
                        _agent.isStopped = false;
                    }
                    SetState(NPCState.Walking);
                }
            }
        }

        private void CheckWorkingTimeout()
        {
            if (_state.value != NPCState.Working) return;

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

        private void ForceResetWorkState()
        {
            _sitStarted = false;
            _isFacingDesk = false;
            _isGoingToDesk = false;
            _postSitPause = false;
            _workingInterrupted = false;

            SetNetBool(_hashSit, false);

            _agent.isStopped = false;
            _workRoutine = null;
            SetState(NPCState.Idle);
        }

        private void SetNetBool(int hash, bool value)
        {
            if (_netAnimator != null)
                _netAnimator.SetBool(hash, value);
            else if (_animator != null)
                _animator.SetBool(hash, value);
        }

        private void UpdateAnimator()
        {
            if (_animator == null) return;

            bool isMoving = !_agent.isStopped &&
                            _agent.hasPath &&
                            _agent.remainingDistance > Mathf.Max(_agent.stoppingDistance, waypointTolerance) &&
                            _agent.velocity.sqrMagnitude > 0.01f;

            bool isIdle = !_isAttacking && !isMoving && _workRoutine == null && !_postSitPause && !_isFacingDesk;

            bool isIdleRelaxed = isIdle &&
                                 (_state.value == NPCState.Idle || _state.value == NPCState.Walking) &&
                                 !_searchingLastSeen && !_patrollingSearch;

            bool isIdleLookAround = isIdle &&
                                    (_state.value == NPCState.Searching || _state.value == NPCState.Returning ||
                                     _patrollingSearch || _searchingLastSeen);
            if (_isAttacking || _workRoutine != null || _postSitPause || _isFacingDesk)
            {
                _targetAxis = Vector2.zero;
                _targetState = 1f;
            }
            else
            {
                _flowAxis = Vector2.ClampMagnitude(
                    _flowAxis + animFlow * Time.deltaTime * (_targetAxis - _flowAxis).normalized, 1f);

                _flowState = Mathf.Clamp01(
                    _flowState + animFlow * Time.deltaTime * Mathf.Sign(_targetState - _flowState));
            }

            float hor = _flowAxis.x;
            float ver = _flowAxis.y;
            float state = Mathf.Clamp01(_flowState);

            if (isServer)
            {
                netHorizontal.value = hor;
                netVertical.value = ver;
                netStateFloat.value = state;
            }

            _animator.SetFloat(_hashHorizontal, netHorizontal.value);
            _animator.SetFloat(_hashVertical, netVertical.value);
            _animator.SetFloat(_hashState, netStateFloat.value);
            _animator.SetBool(_hashJump, _jumpState);

            SetNetBool(_hashSit, _sitStarted);
            SetNetBool(_hashAttack, _isAttacking);
            SetNetBool(_hashSprint, _isSprinting);
            SetNetBool(_hashIdleRelaxed, isIdleRelaxed);
            SetNetBool(_hashIdleLookAround, isIdleLookAround);
        }

        private void UpdateAnimatorTargets(NPCState state)
        {
            if (_isAttacking)
            {
                _targetAxis = Vector2.zero;
                _targetState = 1f;
                _jumpState = false;
                if (_currentTarget != null) lookTarget = _currentTarget;
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

            if (state == NPCState.Searching || state == NPCState.Returning || state == NPCState.Walking)
            {
                _targetAxis = new Vector2(0f, 1f);
                _targetState = state == NPCState.Walking ? 0.45f : (state == NPCState.Returning ? 0.7f : 0.6f);
                _jumpState = false;
                if (_currentTarget != null) lookTarget = _currentTarget;
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

        /// <summary>
        /// Active/désactive l'état d'attaque — plus de doublon avec NetworkAnimator,
        /// UpdateAnimator() s'occupe du SetNetBool chaque frame.
        /// </summary>
        private void SetAttack(bool value)
        {
            _isAttacking = value;
            _agent.isStopped = value;
            if (value) _agent.ResetPath();
            // Pas d'appel manuel à _netAnimator ici : UpdateAnimator() le fait chaque frame.
        }

        // ?????????????????????????????????????????????????????????????
        //  BEHAVIOR TREE
        // ?????????????????????????????????????????????????????????????

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

        // ?????????????????????????????????????????????????????????????
        //  DÉTECTION
        // ?????????????????????????????????????????????????????????????

        private void DetectPlayer()
        {
            _playerTarget = null;
            _currentTarget = null;
            if (_bb != null) _bb.Set(Blackboard.IsPlayerVisible, false);

            Collider[] hits = Physics.OverlapSphere(transform.position, sightRange, playerLayer);
            if (debugDetection) Debug.Log($"[OfficerNPC] Overlap hits = {hits.Length}");

            Transform bestTarget = null;
            float bestDist = float.MaxValue;
            Vector3 eyePos = transform.position + Vector3.up * 1.5f;
            float halfAngle = sightAngle * 0.5f;

            foreach (var hit in hits)
            {
                Transform target = hit.transform.root;
                Vector3 targetPos = target.position + Vector3.up * 1.0f;
                Vector3 toTarget = targetPos - eyePos;
                float dist = toTarget.magnitude;
                if (dist < 0.001f) continue;

                float angle = Vector3.Angle(transform.forward, toTarget.normalized);
                if (angle > halfAngle) continue;

                if (Physics.Raycast(eyePos, toTarget.normalized, dist, obstacleLayer, QueryTriggerInteraction.Ignore))
                    continue;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestTarget = target;
                }
            }

            if (bestTarget != null)
            {
                _playerTarget = bestTarget;
                _currentTarget = bestTarget;
                _bb?.Set(Blackboard.PlayerTransform, _playerTarget);
                _bb?.Set(Blackboard.IsPlayerVisible, true);
                lookTarget = _playerTarget;

                _lastSeenPlayerPos = _playerTarget.position;
                _hasLastSeenPos = true;
                _searchingLastSeen = true;
                _goingToVacuum = true;
                _goingForCockroach = true;

                if (_workRoutine != null || _postSitPause || _isFacingDesk)
                {
                    InterruptWork();
                    return;
                }

                return;
            }

            if (_isAttacking)
            {
                StopAttack();
                BeginLostTargetBehavior();
                return;
            }

            if (_workRoutine != null || _postSitPause || _isFacingDesk)
                return;

            if (_hasLastSeenPos && !_searchingLastSeen && !_patrollingSearch && !_returningToLastSeen)
                BeginSearch();
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

            SetNetBool(_hashSit, false);

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

            if (TryGetReachablePointNear(wallPoint, suctionRange, out _))
            {
                _lastSeenCockroachPos = wallPoint;
                _hasLastSeenCockroach = true;
                _currentTarget = cockroachTarget;
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
            _pathSet = true;
            _state.value = NPCState.Searching;
        }

        private void BeginLostTargetBehavior()
        {
            if (_playerTarget != null)
            {
                if (TryGetReachablePointNear(_playerTarget.position, 2f, out var navPos))
                    _lastSeenPlayerPos = navPos;
                else
                    _lastSeenPlayerPos = _playerTarget.position;

                _hasLastSeenPos = true;
                _searchingLastSeen = true;
            }

            _goingToVacuum = false;
            _goingForCockroach = false;
            _currentTarget = null;

            if (_hasVacuum && _hasLastSeenPos)
            {
                SetSearchWalk();
                _agent.isStopped = false;
                _agent.SetDestination(_lastSeenPlayerPos);
                _pathSet = true;
                SetState(NPCState.Searching);
            }
            else
            {
                SetWalk();
                _agent.isStopped = false;
                SetState(NPCState.Walking);
            }
        }

        // ?????????????????????????????????????????????????????????????
        //  CONDITIONS BT
        // ?????????????????????????????????????????????????????????????

        private bool IsPlayerVisible() => _bb.Get<bool>(Blackboard.IsPlayerVisible);
        private bool HasVacuum() => _hasVacuum;
        private bool NeedVacuum() => !_hasVacuum && _goingToVacuum;
        private bool IsReturningToLastSeen() => _hasVacuum && _returningToLastSeen;
        private bool HasLastSeenPosition() => _hasLastSeenPos && _hasVacuum && _searchingLastSeen;
        private bool CanSearchPatrol() => _hasVacuum && _patrollingSearch;
        private bool NeedReturnVacuum() => _hasVacuum && _returningVacuum;
        private bool HasDeskTarget() => assignedDesk != null && _inWorkPhase;

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

        // ?????????????????????????????????????????????????????????????
        //  ACTIONS BT
        // ?????????????????????????????????????????????????????????????

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
            _pathSet = true;
            SetState(NPCState.Returning);

            if (IsAtTransform(_lastSeenPlayerPos))
            {
                _returningToLastSeen = false;
                _searchingLastSeen = true;
                _searchEndTime = Time.time + searchDuration;
                _waitUntilTime = Time.time + searchWaitDuration;
                _waitingAtPoint = true;
                _agent.isStopped = true;
                _agent.ResetPath();
                SetSearchWalk();
                SetState(NPCState.Searching);
                return NodeStatus.Success;
            }

            return NodeStatus.Running;
        }

        private NodeStatus GoToLastSeen()
        {
            if (!_hasLastSeenPos) return NodeStatus.Failure;

            if (_waitingAtPoint)
            {
                if (Time.time < _waitUntilTime)
                {
                    _agent.isStopped = true;
                    SetState(NPCState.Searching);
                    return NodeStatus.Running;
                }

                _waitingAtPoint = false;
            }

            _agent.isStopped = false;
            SetSearchWalk();

            if (!_pathSet || !_agent.hasPath)
            {
                _agent.SetDestination(_lastSeenPlayerPos);
                _pathSet = true;
            }

            SetState(NPCState.Searching);

            if (IsAtTransform(_lastSeenPlayerPos))
            {
                _waitUntilTime = Time.time + searchWaitDuration;
                _waitingAtPoint = true;
                _agent.isStopped = true;
                _agent.ResetPath();
                return NodeStatus.Success;
            }

            return NodeStatus.Running;
        }

        private NodeStatus SearchArea()
        {
            if (_workRoutine != null || _postSitPause) return NodeStatus.Failure;

            if (_waitingAtPoint)
            {
                _agent.isStopped = true;
                if (Time.time < _waitUntilTime)
                {
                    SetState(NPCState.Searching);
                    return NodeStatus.Running;
                }

                _waitingAtPoint = false;
                _pathSet = false;
            }

            _agent.isStopped = false;

            if (Time.time < _searchEndTime)
            {
                SetSearchWalk();
                SetState(NPCState.Searching);

                if (!_pathSet || IsAtDestination())
                {
                    _agent.SetDestination(_lastSeenPlayerPos);
                    _pathSet = true;
                    _waitUntilTime = Time.time + searchWaitDuration;
                    _waitingAtPoint = true;
                }

                return NodeStatus.Running;
            }

            _searchingLastSeen = false;
            _patrollingSearch = true;
            _patrolSearchEndTime = Time.time + patrolSearchDuration;
            _pathSet = false;
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
                _pathSet = false;
                return NodeStatus.Success;
            }

            if (_waitingAtPoint)
            {
                _agent.isStopped = true;
                if (Time.time < _waitUntilTime)
                {
                    SetWalk();
                    SetState(NPCState.Walking);
                    return NodeStatus.Running;
                }

                _waitingAtPoint = false;
                _pathSet = false;
            }

            int idx = _bb.Get<int>(Blackboard.PatrolIndex);

            if (!_agent.pathPending && (!_agent.hasPath || IsAtDestination() || !_pathSet))
            {
                if (_currentPatrolIndex != idx)
                    _currentPatrolIndex = idx;

                idx = (idx + 1) % patrolPoints.Length;
                _bb.Set(Blackboard.PatrolIndex, idx);
                _agent.SetDestination(patrolPoints[idx].position);
                _pathSet = true;
            }

            if (IsAtTransform(patrolPoints[idx].position))
            {
                _waitUntilTime = Time.time + pointWaitDuration;
                _waitingAtPoint = true;
                _agent.isStopped = true;
                _agent.ResetPath();
                SetWalk();
                SetState(NPCState.Walking);
                return NodeStatus.Running;
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
                _currentTarget = null;
                _pathSet = false;
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
            _isSprinting = false;

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

            Vector3 eyePos = transform.position + Vector3.up * 1.5f;
            Vector3 targetPos = _playerTarget.position + Vector3.up * 1.0f;
            Vector3 toTarget = targetPos - eyePos;
            float dist = toTarget.magnitude;

            if (dist > vacuumRange || Vector3.Angle(transform.forward, toTarget.normalized) > sightAngle * 0.5f)
            {
                StopAttack();
                _nextAttackTime = Time.time + attackCooldown;
                BeginLostTargetBehavior();
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
                BeginLostTargetBehavior();
                return NodeStatus.Success;
            }

            return NodeStatus.Running;
        }

        [TargetRpc]
        private void ApplyVacuumForceToPlayer(PlayerID target, Vector3 force)
        {
            if (_playerTarget == null) return;

            if (_playerTarget.TryGetComponent<PlayerVacuumReceiver>(out var receiver))
                receiver.ReceiveVacuumForce(force,vacuumObject.transform);
        }

        private void StopAttack()
        {

            _isAttacking = false;
            SetAttack(false);
            _agent.isStopped = false;
        }

        // ?????????????????????????????????????????????????????????????
        //  TRAVAIL AU BUREAU
        // ?????????????????????????????????????????????????????????????

        private void EndWork()
        {
            _sitStarted = false;
            _isFacingDesk = false;
            _isGoingToDesk = false;
            _postSitPause = false;
            _workingInterrupted = false;

            SetNetBool(_hashSit, false);

            _agent.isStopped = false;
            _workRoutine = null;

            _inWorkPhase = false;
            _phaseEndTime = Time.time + patrolPhaseDuration;

            SetState(NPCState.Walking);

            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                int idx = _bb.Get<int>(Blackboard.PatrolIndex);
                idx = (idx + 1) % patrolPoints.Length;
                _bb.Set(Blackboard.PatrolIndex, idx);
                _agent.isStopped = false;
                SetWalk();
                _agent.SetDestination(patrolPoints[idx].position);
                _pathSet = true;
            }
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
                if (_workingInterrupted) yield break;

                if (!_inWorkPhase)
                {
                    _sitStarted = false;
                    SetNetBool(_hashSit, false);
                    _postSitPause = false;
                    _workRoutine = null;
                    _isFacingDesk = false;
                    _agent.isStopped = false;
                    SetState(NPCState.Walking);
                    yield break;
                }

                timer += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(sitOutDuration);

            _sitStarted = false;
            SetNetBool(_hashSit, false);

            _postSitPause = true;
            yield return new WaitForSeconds(pauseAfterWork);
            _postSitPause = false;

            EndWork();
        }

        private NodeStatus TickTask() => _workRoutine != null ? NodeStatus.Running : NodeStatus.Success;

        // ?????????????????????????????????????????????????????????????
        //  PATROUILLE
        // ?????????????????????????????????????????????????????????????

        private NodeStatus Patrol()
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
                return NodeStatus.Failure;

            if (_workRoutine != null || _postSitPause || _isFacingDesk)
                return NodeStatus.Running;

            if (_waitingAtPoint)
            {
                _agent.isStopped = true;
                if (Time.time < _waitUntilTime)
                {
                    SetWalk();
                    SetState(NPCState.Walking);
                    return NodeStatus.Running;
                }

                _waitingAtPoint = false;
                _pathSet = false;
            }

            int idx = _bb.Get<int>(Blackboard.PatrolIndex);

            if (!_agent.pathPending && (!_agent.hasPath || IsAtDestination() || !_pathSet))
            {
                idx = (idx + 1) % patrolPoints.Length;
                _bb.Set(Blackboard.PatrolIndex, idx);
                _agent.SetDestination(patrolPoints[idx].position);
                _pathSet = true;
            }

            if (IsAtTransform(patrolPoints[idx].position))
            {
                _waitUntilTime = Time.time + pointWaitDuration;
                _waitingAtPoint = true;
                _agent.isStopped = true;
                _agent.ResetPath();
                SetWalk();
                SetState(NPCState.Walking);
                return NodeStatus.Running;
            }

            SetWalk();
            SetState(NPCState.Walking);
            return NodeStatus.Running;
        }

        // ?????????????????????????????????????????????????????????????
        //  HELPERS
        // ?????????????????????????????????????????????????????????????

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