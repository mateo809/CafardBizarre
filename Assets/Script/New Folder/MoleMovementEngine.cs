using PurrNet;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
#if STEAMWORKS_NET
using Steamworks;
#endif

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class RoachController1 : NetworkBehaviour
{
    public enum PlayerState { Grounded, Jumping, Falling, Gliding, WallClimbing, Carrying, InVehicle }
    public PlayerState _currentState = PlayerState.Grounded;

    [Header("Movement Settings")]
    public float walkingSpeed = 7.5f;
    public float jumpVelocity = 6.5f;
    public float snapDownDist = 1.0f;
    public float skinWidth = 0.03f;
    public int maxBounces = 6;
    public LayerMask collisionMask = ~0;

    private PlayerState _previousState;

    [Header("Audio")]
    [SerializeField] private float stepInterval = 0.4f;
    private float stepTimer;

    [Header("Sprint")]
    [SerializeField] private float _sprintMultiplier = 1.5f;

    [Header("Gravity / Glide")]
    [SerializeField] private float _gravityStrength = 25f;
    [SerializeField] private float _glideGravity = 6f;
    [SerializeField] private float _glideFallRate = -1.5f;
    [SerializeField] private float _fallGravityMultiplier = 1.35f;

    [Header("Jump Tuning")]
    [SerializeField] private float _jumpCooldown = 0.35f;
    [SerializeField] private float _coyoteTime = 0.15f;
    [SerializeField] private float _jumpBufferTime = 0.2f;
    [SerializeField] private float _jumpPowerMultiplier = 1.18f;
    [SerializeField] private float _jumpHorizontalBoost = 0.15f;
    [SerializeField] private float _jumpGraceVelocityClamp = -2f;
    [SerializeField] private float _jumpApexGravityMultiplier = 0.75f;
    [SerializeField] private float _jumpCutGravityMultiplier = 2.2f;

    [Header("Slope")]
    [SerializeField] private float _maxSlopeAngle = 80f;

    [Header("Camera & Input")]
    public Transform cameraRig;
    public InputActionReference moveAction;
    public InputActionReference jumpAction;
    public InputActionReference sprintAction;
    public InputActionReference pickUpAction;
    public InputActionReference dropAction;

    [Header("Visual Smoothing")]
    [Range(1f, 30f)] public float rotationSmoothing = 15f;
    [SerializeField] private float _strafeRotationSpeed = 8f;
    [SerializeField] private float _alignSpeed = 8f;

    [Header("Carrying")]
    [SerializeField] private float _maxCarryWeightSlowdown = 0.5f;
    [SerializeField] private Transform _pickupOrigin;
    [SerializeField] private Transform _transportPoint;
    [SerializeField] private float _pickupRange = 3f;
    [SerializeField] private LayerMask _itemLayer = ~0;

    [Header("Network Animation")]
    [SerializeField] private NetworkAnimator _networkAnimator;

    [Header("UI Feedback")]
    [SerializeField] private GameObject pickupFeedbackPrefab;
    [SerializeField] private GameObject carryingFeedbackPrefab;

    [Header("Camera Prefab")]
    public GameObject cameraPrefab;

    [Header("Véhicule")]
    public InputActionReference enterVehicleAction;
    public InputActionReference exitVehicleAction;
    public InputActionReference brakeAction;
    public float enterVehicleDistance = 4f;
    public LayerMask vehicleLayer;

    [Header("Anti-Stuck")]
    [SerializeField] private float _stuckCheckInterval = 0.4f;
    [SerializeField] private float _stuckDistThreshold = 0.05f;
    [SerializeField] private float _stuckEjectForce = 6f;
    [SerializeField] private int _stuckMaxAttempts = 3;

    public SyncVar<string> steamId = new SyncVar<string>("0");

    private Rigidbody rb;
    private SphereCollider sc;
    private BackendCaller _backendCaller;

    private Vector3 groundNormal = Vector3.up;
    private Vector3 worldVelocity;
    private bool isGrounded;
    private bool wasGrounded;

    private float jumpCooldownTimer;
    private readonly RaycastHit[] hitCache = new RaycastHit[16];

    private Vector3 lastStableForward;
    private Vector3 lastStableRight;
    private Vector3 lastGroundNormal;

    private Vector2 _moveInput;
    private bool _jumpPressed;
    private bool _jumpHeld;
    private bool _sprintHeld;

    private float _lastGroundedTime;
    private float _jumpBufferTimer;
    private float _nextJumpTime;

    private Vector3 _lastMoveDirection = Vector3.forward;

    private GameObject _carriedItem;
    private NetworkIdentity _carriedItemNetworkId;
    private bool _isCarryingObject;
    private float _currentWeight;
    private float _baseWalkSpeed;

    public GameObject carriedObject;

    private GameObject _pickupFeedbackInstance;
    private GameObject _carryingFeedbackInstance;
    private Canvas _mainCanvas;
    private bool _isInPickupRange;

    private Vector3 _lastStuckCheckPos;
    private float _stuckCheckTimer;
    private int _stuckAttemptCount;

    private CarControl _currentVehicle;
    private int _currentSeat = -1;
    private bool _isDriver => _currentSeat == 0;
    private bool _isInVehicle => _currentVehicle != null;

    private Collider[] _playerColliders;
    private bool _savedKinematic;
    private bool _savedDetectCollisions;

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);
        if (asServer || !isOwner) return;

        string localSteamId = GetLocalSteamId();
        SendSteamIdServerRpc(localSteamId);

        if (cameraPrefab != null)
        {
            GameObject camInstance = Instantiate(cameraPrefab);
            CameraController camController = camInstance.GetComponent<CameraController>();
            if (camController != null)
                camController.Setup(transform, this);
            cameraRig = camInstance.transform;
        }

        if (_backendCaller != null)
            StartCoroutine(_backendCaller.GetMe());

    }

    private string GetLocalSteamId()
    {
#if STEAMWORKS_NET
        try { return SteamUser.GetSteamID().m_SteamID.ToString(); }
        catch { }
#endif
        return "0";
    }

    [ServerRpc]
    private void SendSteamIdServerRpc(string id) => SyncSteamIdObserversRpc(id);

    [ObserversRpc]
    private void SyncSteamIdObserversRpc(string id) => steamId.value = id;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sc = GetComponent<SphereCollider>();
        _backendCaller = GetComponent<BackendCaller>();

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        groundNormal = Vector3.up;
        lastStableForward = transform.forward;
        lastStableRight = transform.right;
        lastGroundNormal = groundNormal;

        _playerColliders = GetComponentsInChildren<Collider>(true);
    }

    private void Start()
    {
        _baseWalkSpeed = walkingSpeed;
        _lastStuckCheckPos = transform.position;

        if (isOwner)
            StartCoroutine(FindCanvasWithRetry());

        if (_backendCaller != null)
            _backendCaller.GetMe();
    }

    private void OnEnable()
    {
        moveAction?.action.Enable();
        jumpAction?.action.Enable();
        sprintAction?.action.Enable();
        pickUpAction?.action.Enable();
        dropAction?.action.Enable();
        enterVehicleAction?.action.Enable();
        exitVehicleAction?.action.Enable();
        brakeAction?.action.Enable();
    }

    private void OnDisable()
    {
        moveAction?.action.Disable();
        jumpAction?.action.Disable();
        sprintAction?.action.Disable();
        pickUpAction?.action.Disable();
        dropAction?.action.Disable();
        enterVehicleAction?.action.Disable();
        exitVehicleAction?.action.Disable();
        brakeAction?.action.Disable();
    }

    private void Update()
    {
        if (!isOwner) return;

        if (_isInVehicle)
        {
            if (exitVehicleAction != null && exitVehicleAction.action.WasPressedThisFrame())
                HandleVehicleExit();

            if (_isDriver)
                HandleDriverInput();

            return;
        }

        _moveInput = moveAction?.action.ReadValue<Vector2>() ?? Vector2.zero;

        if (enterVehicleAction != null && enterVehicleAction.action.WasPressedThisFrame())
            HandleVehicleEnter();

        if (pickUpAction != null && pickUpAction.action.WasPressedThisFrame())
            OnPickUpPressed();

        if (dropAction != null && dropAction.action.WasPressedThisFrame())
            OnDropPressed();

        if (jumpAction != null && jumpAction.action.WasPressedThisFrame())
        {
            _jumpPressed = true;
            _jumpHeld = true;
        }

        if (jumpAction != null && !jumpAction.action.IsPressed())
            _jumpHeld = false;

        if (sprintAction != null)
            _sprintHeld = sprintAction.action.IsPressed();

        if (_jumpPressed) _jumpBufferTimer = _jumpBufferTime;
        else _jumpBufferTimer -= Time.deltaTime;

        if (isGrounded) _lastGroundedTime = Time.time;
    }

    private void LateUpdate()
    {
        // Suivi du siège chaque frame : colle le joueur sur le siège sans SetParent
        // Fonctionne même si la voiture bouge, tourne ou est secouée
        if (!_isInVehicle || _currentVehicle == null) return;

        Transform seat = _currentVehicle.GetSeatTransform(_currentSeat);
        if (seat == null) return;

        rb.position = seat.position;
        rb.rotation = seat.rotation;
        transform.SetPositionAndRotation(seat.position, seat.rotation);
    }

    private void FixedUpdate()
    {
        if (!isOwner) return;
        if (_isInVehicle) return;

        float angle = Vector3.Angle(lastGroundNormal, groundNormal);
        if (angle > 45f) ResetMovementAxes();
        lastGroundNormal = groundNormal;

        jumpCooldownTimer -= Time.fixedDeltaTime;

        if (isGrounded)
        {
            float pen = Vector3.Dot(worldVelocity, groundNormal);
            if (pen < 0f) worldVelocity -= pen * groundNormal;
        }

        HandleGravityAndJump();

        Vector3 moveInput3D = new Vector3(_moveInput.x, 0f, _moveInput.y);
        float speed = walkingSpeed;
        if (_sprintHeld && isGrounded && _carriedItem == null) speed *= _sprintMultiplier;

        Vector3 surfaceDir = ComputeSurfaceDirection(moveInput3D);
        if (surfaceDir.magnitude > 0.001f)
            _lastMoveDirection = surfaceDir;

        Vector3 movement = surfaceDir * speed * Time.fixedDeltaTime;
        Vector3 totalMove = movement + worldVelocity * Time.fixedDeltaTime;

        if (totalMove.magnitude > 0.001f)
            MoveWithBounces(totalMove);

        wasGrounded = isGrounded;
        CheckGrounded();

        if (isGrounded && jumpCooldownTimer <= 0f)
            SnapToSurface();

        if (isGrounded)
        {
            float pen = Vector3.Dot(worldVelocity, groundNormal);
            if (pen < 0f) worldVelocity -= pen * groundNormal;

            Vector3 horizontal = Vector3.ProjectOnPlane(worldVelocity, groundNormal);
            bool justLanded = wasGrounded == false;
            float drainRate = justLanded ? 20f : 12f;
            worldVelocity -= horizontal * Mathf.Min(1f, drainRate * Time.fixedDeltaTime);
        }

        HandleAntiStuck();
        _previousState = _currentState;
        UpdateState();
        HandleGlideAudio();
        UpdateRotation();
        UpdateAnimations();
        CheckPickupRangeFeedback();
        UpdateCarryingFeedback();
        HandleFootsteps();
        _jumpPressed = false;
    }

    private void HandleGlideAudio()
    {
        bool wasGliding = _previousState == PlayerState.Gliding;
        bool isGliding = _currentState == PlayerState.Gliding;

        if (!wasGliding && isGliding)
        {
            AudioController.Instance.PlayLoopedSound(
                AudioType.Fly,
                AudioSourceType.Player
            );
        }

        if (wasGliding && !isGliding)
        {
            AudioController.Instance.StopSound(AudioType.Fly);
        }
    }

    private void HandleFootsteps()
    {
        if (!isOwner) return; // important : seul le joueur local envoie

        if (!isGrounded) return;
        if (_moveInput.sqrMagnitude < 0.1f) return;

        stepTimer -= Time.fixedDeltaTime;

        if (stepTimer <= 0f)
        {
            stepTimer = stepInterval;

            PlayStepServerRpc(transform.position);
        }
    }

    private void HandleVehicleEnter()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, enterVehicleDistance, vehicleLayer);
        if (hits == null || hits.Length == 0) return;

        CarControl closest = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var v = hit.GetComponentInParent<CarControl>();
            if (v == null) continue;
            float d = Vector3.Distance(transform.position, v.transform.position);
            if (d < closestDist)
            {
                closestDist = d;
                closest = v;
            }
        }

        if (closest == null) return;
        closest.Server_TrySit(this);
    }

    private void HandleVehicleExit()
    {
        if (_currentVehicle != null)
            _currentVehicle.Server_TryExit(this);
    }

    private void HandleDriverInput()
    {
        if (_currentVehicle == null) return;

        Vector2 move = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        bool brake = brakeAction != null && brakeAction.action.IsPressed();

        _currentVehicle.Server_SendDriverInput(move.y, move.x, brake);
    }

    public void OnEnteredVehicle(CarControl vehicle, int seatIndex)
    {
        _currentVehicle = vehicle;
        _currentSeat = seatIndex;
        _currentState = PlayerState.InVehicle;

        // 1. Désactiver physique EN PREMIER (colliders off, rb kinematic, vélocités = 0)
        DisablePlayerPhysics();

        // 2. Snap immédiat au siège — PAS de SetParent pour éviter les bugs rb/interpolation
        //    Le suivi en temps réel est assuré par LateUpdate via _currentVehicle
        Transform seat = vehicle.GetSeatTransform(seatIndex);
        if (seat != null)
        {
            rb.position = seat.position;
            rb.rotation = seat.rotation;
            transform.SetPositionAndRotation(seat.position, seat.rotation);
        }

        if (_networkAnimator != null)
            _networkAnimator.enabled = false;

        var animator = GetComponentInChildren<Animator>();
        if (animator != null)
            animator.enabled = false;
    }

    public void OnExitedVehicle(Vector3 exitPosition)
    {
        // 1. Reset de l'état AVANT de réactiver quoi que ce soit
        var prevVehicle = _currentVehicle;
        _currentVehicle = null;
        _currentSeat = -1;
        _currentState = PlayerState.Grounded;

        // 2. Réactiver la physique (colliders on, rb redevient kinematic comme avant)
        EnablePlayerPhysics();

        // 3. Placer proprement via rb.position + transform pour bypasser l'interpolation
        //    On force une rotation propre (X=0, Z=0) pour ne pas hériter de la rotation du siège
        Quaternion cleanRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        rb.position = exitPosition;
        rb.rotation = cleanRot;
        transform.SetPositionAndRotation(exitPosition, cleanRot);

        // 4. Reset mouvement complet
        isGrounded = false;
        groundNormal = Vector3.up;
        worldVelocity = Vector3.zero;
        lastStableForward = transform.forward;
        lastStableRight = transform.right;
        lastGroundNormal = Vector3.up;

        if (_networkAnimator != null)
            _networkAnimator.enabled = true;

        var animator = GetComponentInChildren<Animator>();
        if (animator != null)
            animator.enabled = true;
    }

    private void DisablePlayerPhysics()
    {
        if (rb == null) return;

        _savedKinematic = rb.isKinematic;
        _savedDetectCollisions = rb.detectCollisions;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.detectCollisions = false;

        foreach (var col in _playerColliders)
            if (col != null) col.enabled = false;
    }

    private void EnablePlayerPhysics()
    {
        if (rb == null) return;

        rb.isKinematic = _savedKinematic;
        rb.detectCollisions = _savedDetectCollisions;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        foreach (var col in _playerColliders)
            if (col != null) col.enabled = true;
    }

    private void HandleAntiStuck()
    {
        _stuckCheckTimer += Time.fixedDeltaTime;
        if (_stuckCheckTimer < _stuckCheckInterval) return;
        _stuckCheckTimer = 0f;

        bool hasInput = _moveInput.sqrMagnitude > 0.01f;
        float moved = Vector3.Distance(rb.position, _lastStuckCheckPos);
        _lastStuckCheckPos = rb.position;

        if (!hasInput || moved >= _stuckDistThreshold)
        {
            _stuckAttemptCount = 0;
            return;
        }

        _stuckAttemptCount++;
        Vector3 ejectDir = ComputeEjectDirection();
        if (ejectDir.sqrMagnitude < 0.01f) ejectDir = Vector3.up;

        if (_stuckAttemptCount >= _stuckMaxAttempts)
        {
            rb.MovePosition(rb.position + Vector3.up * (sc.radius * 1.5f));
            worldVelocity = Vector3.up * 2f;
            groundNormal = Vector3.up;
            isGrounded = false;
            _stuckAttemptCount = 0;
        }
        else
        {
            worldVelocity += ejectDir * _stuckEjectForce;
        }
    }

    private Vector3 ComputeEjectDirection()
    {
        Vector3 origin = rb.position + transform.rotation * sc.center;
        float probeR = sc.radius * 2.2f;
        Vector3 sumNormal = Vector3.zero;
        int count = 0;

        Vector3[] probes = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };

        foreach (var dir in probes)
        {
            int hits = Physics.SphereCastNonAlloc(origin, sc.radius, dir, hitCache, probeR, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                var h = hitCache[i];
                if (h.collider == null || h.collider.transform == transform) continue;
                sumNormal += h.normal;
                count++;
            }
        }

        if (count == 0) return Vector3.zero;
        return (sumNormal / count).normalized;
    }

    private void MoveWithBounces(Vector3 movement)
    {
        Vector3 remainingMomentum = movement;
        Vector3 currentPos = rb.position;

        for (int i = 0; i < maxBounces && remainingMomentum.magnitude > 0.0001f; i++)
        {
            float dist = remainingMomentum.magnitude;
            Vector3 dir = remainingMomentum.normalized;

            if (SphereCast(dir, dist + skinWidth, out RaycastHit hit))
            {
                float moveDist = Mathf.Max(0, hit.distance - skinWidth);
                currentPos += dir * moveDist;

                Vector3 normal = hit.normal;
                float remainingDist = Mathf.Max(0f, dist - moveDist);
                Vector3 slideDir = Vector3.ProjectOnPlane(dir, normal);

                if (slideDir.sqrMagnitude < 0.001f)
                {
                    currentPos += normal * skinWidth;
                    break;
                }

                remainingMomentum = slideDir.normalized * remainingDist;
                groundNormal = normal;
            }
            else
            {
                currentPos += remainingMomentum;
                break;
            }
        }

        rb.MovePosition(currentPos);
    }

    private Vector3 ComputeSurfaceDirection(Vector3 inputDir)
    {
        if (inputDir.sqrMagnitude < 0.001f) return Vector3.zero;

        float surfaceAngle = Vector3.Angle(groundNormal, Vector3.up);

        if (surfaceAngle <= 45f && cameraRig != null)
        {
            Vector3 camForward = Vector3.ProjectOnPlane(cameraRig.forward, groundNormal).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(cameraRig.right, groundNormal).normalized;

            if (camForward.sqrMagnitude > 0.01f)
                return (camForward * inputDir.z + camRight * inputDir.x).normalized;
        }

        Vector3 surfaceForward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
        Vector3 surfaceRight = Vector3.ProjectOnPlane(transform.right, groundNormal).normalized;

        if (surfaceForward.sqrMagnitude < 0.01f)
            surfaceForward = Vector3.Cross(groundNormal, transform.right).normalized;

        return (surfaceForward * inputDir.z + surfaceRight * inputDir.x).normalized;
    }

    private void CheckGrounded()
    {
        RaycastHit bestHit = default;
        float bestDist = float.MaxValue;
        bool found = false;

        Vector3[] dirs = { -groundNormal, Vector3.down };
        foreach (var dir in dirs)
        {
            Vector3 origin = rb.position + transform.rotation * sc.center;
            int count = Physics.SphereCastNonAlloc(origin, sc.radius, dir, hitCache, skinWidth * 6f, collisionMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                var h = hitCache[i];
                if (h.collider == null || h.collider.transform == transform) continue;
                if (h.distance <= 0f) continue;
                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    bestHit = h;
                    found = true;
                }
            }
        }

        isGrounded = found;
        if (found) groundNormal = bestHit.normal;
    }

    private void SnapToSurface()
    {
        const float snapEpsilon = 0.02f;

        if (SphereCast(-groundNormal, snapDownDist, out RaycastHit hit1))
        {
            float snapDist = hit1.distance - skinWidth;
            if (snapDist > snapEpsilon)
                rb.MovePosition(rb.position + (-groundNormal * snapDist));
            groundNormal = hit1.normal;
            isGrounded = true;
            return;
        }

        if (SphereCast(Vector3.down, snapDownDist * 0.5f, out RaycastHit hit2))
        {
            float snapDist = hit2.distance - skinWidth;
            if (snapDist > snapEpsilon)
                rb.MovePosition(rb.position + (Vector3.down * snapDist));
            groundNormal = hit2.normal;
            isGrounded = true;
        }
    }

    private bool SphereCast(Vector3 dir, float dist, out RaycastHit closestHit)
    {
        closestHit = new RaycastHit();
        Vector3 origin = rb.position + transform.rotation * sc.center;
        int hits = Physics.SphereCastNonAlloc(origin, sc.radius, dir, hitCache, dist, collisionMask, QueryTriggerInteraction.Ignore);

        float minDst = float.MaxValue;
        bool found = false;

        for (int i = 0; i < hits; i++)
        {
            if (hitCache[i].collider == null) continue;
            if (hitCache[i].collider.transform == transform) continue;
            if (hitCache[i].distance < minDst)
            {
                minDst = hitCache[i].distance;
                closestHit = hitCache[i];
                found = true;
            }
        }

        return found;
    }

    private void ResetMovementAxes()
    {
        if (cameraRig == null) return;

        Vector3 newForward = Vector3.ProjectOnPlane(cameraRig.forward, groundNormal).normalized;
        if (newForward.sqrMagnitude < 0.001f)
            newForward = Vector3.Cross(groundNormal, cameraRig.right).normalized;

        lastStableForward = newForward;
        lastStableRight = Vector3.Cross(newForward, groundNormal).normalized;
    }

    private void HandleGravityAndJump()
    {
        if (!isGrounded)
        {
            float g = (_currentState == PlayerState.Gliding) ? _glideGravity : _gravityStrength;

            if (_jumpHeld && worldVelocity.y > 0f)
                g *= _jumpApexGravityMultiplier;
            else if (!_jumpHeld && worldVelocity.y > 0f)
                g *= _jumpCutGravityMultiplier;
            else if (worldVelocity.y < 0f)
                g *= _fallGravityMultiplier;

            worldVelocity += Vector3.down * g * Time.fixedDeltaTime;
            groundNormal = Vector3.Slerp(groundNormal, Vector3.up, Time.fixedDeltaTime * 3f);
        }

        bool canJump = isGrounded || (Time.time - _lastGroundedTime) <= _coyoteTime;

        if (_jumpBufferTimer > 0f && canJump && Time.time >= _nextJumpTime && _carriedItem == null)
        {
            isGrounded = false;
            Vector3 jumpDir = groundNormal.normalized;

            if (Vector3.Dot(worldVelocity, jumpDir) > 0f)
                worldVelocity = Vector3.ProjectOnPlane(worldVelocity, jumpDir);

            worldVelocity += jumpDir * (jumpVelocity * _jumpPowerMultiplier);

            Vector3 horizontalBoost = ComputeSurfaceDirection(new Vector3(_moveInput.x, 0f, _moveInput.y)) * _jumpHorizontalBoost;
            worldVelocity += horizontalBoost;

            if (Vector3.Dot(worldVelocity, Vector3.up) < _jumpGraceVelocityClamp)
                worldVelocity.y = _jumpGraceVelocityClamp;

            _jumpBufferTimer = 0f;
            _nextJumpTime = Time.time + _jumpCooldown;
            jumpCooldownTimer = _jumpCooldown;
        }

        if (!_jumpHeld && worldVelocity.y > 0f)
            worldVelocity.y = Mathf.MoveTowards(worldVelocity.y, 0f, _gravityStrength * 2.5f * Time.fixedDeltaTime);
    }

    private void UpdateState()
    {
        if (_isInVehicle) { _currentState = PlayerState.InVehicle; return; }
        if (_isCarryingObject) { _currentState = PlayerState.Carrying; return; }

        if (isGrounded) _currentState = PlayerState.Grounded;
        else
        {
            float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
            if (slopeAngle > _maxSlopeAngle) _currentState = PlayerState.WallClimbing;
            else if (_jumpHeld) _currentState = PlayerState.Gliding;
            else _currentState = PlayerState.Falling;
        }
    }

    private void UpdateRotation()
    {
        if (_isInVehicle) return;

        Vector3 up = groundNormal;
        float dt = Time.fixedDeltaTime;
        float surfaceAngle = Vector3.Angle(groundNormal, Vector3.up);
        bool isWallLike = surfaceAngle > 45f;

        if (_moveInput.sqrMagnitude > 0.001f)
        {
            Vector3 forward = Vector3.ProjectOnPlane(_lastMoveDirection, up);
            if (forward.sqrMagnitude < 0.001f) return;

            forward = forward.normalized;
            Quaternion target = Quaternion.LookRotation(forward, up);
            float speed = isWallLike ? rotationSmoothing : rotationSmoothing * 2f;
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, target, speed * dt));
        }
        else
        {
            Vector3 currentForward = isWallLike
                ? Vector3.ProjectOnPlane(Vector3.up, groundNormal).normalized
                : Vector3.ProjectOnPlane(transform.forward, up).normalized;

            if (currentForward.sqrMagnitude < 0.01f)
                currentForward = Vector3.Cross(up, transform.right).normalized;

            Quaternion target = Quaternion.LookRotation(currentForward, up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, target, _alignSpeed * dt));
        }
    }

    private void UpdateAnimations()
    {
        if (_networkAnimator == null) return;
        if (_isInVehicle) return;

        bool carrying = _isCarryingObject || _carriedItem != null;
        bool moving = _moveInput.sqrMagnitude > 0.01f;
        bool grounded = isGrounded;
        bool wallClimb = !grounded && Vector3.Angle(groundNormal, Vector3.up) > _maxSlopeAngle;

        _networkAnimator.SetBool("Item", carrying);
        _networkAnimator.SetBool("Run", grounded && moving && !carrying);
        _networkAnimator.SetBool("Fly", !grounded && !wallClimb && !carrying);
    }

    private void OnPickUpPressed()
    {
        if (_carriedItem != null) return;
        if (_pickupOrigin == null) _pickupOrigin = transform;

        if (Physics.Raycast(_pickupOrigin.position, _pickupOrigin.forward, out RaycastHit hit, _pickupRange, _itemLayer, QueryTriggerInteraction.Ignore))
        {
            ItemPickUp itemPickUp = hit.collider.GetComponentInParent<ItemPickUp>();
            if (itemPickUp != null)
                PickUpItemServerRPC(itemPickUp.gameObject);
        }
    }

    private void OnDropPressed()
    {
        if (_carriedItem == null) return;
        DropItemServerRPC();
    }

    [ServerRpc]
    private void PickUpItemServerRPC(GameObject item)
    {
        if (item != null) PickUpItemObserverRPC(item);
    }

    [ObserversRpc]
    private void PickUpItemObserverRPC(GameObject item)
    {
        if (item == null) return;

        if (item.TryGetComponent(out Rigidbody itemRb))
        {
            itemRb.isKinematic = true;
            itemRb.detectCollisions = false;
            itemRb.linearVelocity = Vector3.zero;
            itemRb.angularVelocity = Vector3.zero;
        }

        if (item.TryGetComponent(out ItemPickUp itemPickUp))
        {
            itemPickUp.Interact(gameObject);
            _currentWeight = itemPickUp.weight;
        }

        Transform anchor = (_transportPoint != null) ? _transportPoint : transform;

        Vector3 worldScale = item.transform.lossyScale;
        item.transform.SetParent(anchor, false);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;

        Vector3 parentScale = anchor.lossyScale;
        item.transform.localScale = new Vector3(
            parentScale.x != 0f ? worldScale.x / parentScale.x : worldScale.x,
            parentScale.y != 0f ? worldScale.y / parentScale.y : worldScale.y,
            parentScale.z != 0f ? worldScale.z / parentScale.z : worldScale.z
        );

        carriedObject = item;
        _carriedItem = item;
        _isCarryingObject = true;
        _currentState = PlayerState.Carrying;
        _carriedItemNetworkId = item.GetComponent<NetworkIdentity>();

        ApplyCarrySpeedModifier();

        AudioController.Instance.PlaySound(
    AudioType.PickItem,
    AudioSourceType.Player,
    transform.position
);
    }

    [ServerRpc]
    private void PlayStepServerRpc(Vector3 pos)
    {
        PlayStepObserversRpc(pos);
    }

    [ObserversRpc]
    private void PlayStepObserversRpc(Vector3 pos)
    {
        if (AudioController.Instance == null) return;

        AudioController.Instance.PlaySound(
            AudioType.Step,
            AudioSourceType.Player,
            pos
        );
    }

    [ServerRpc]
    private void DropItemServerRPC() => DropItemObserverRPC();

    [ObserversRpc]
    private void DropItemObserverRPC()
    {
        if (_carriedItem == null) return;

        _isCarryingObject = false;

        ItemPickUp itemPickUp = _carriedItem.GetComponent<ItemPickUp>();
        if (TryGetComponent(out InventoryManager inventory) && itemPickUp != null)
        {
            inventory.RemoveItem(itemPickUp.itemData, 1);
            itemPickUp.owner = null;
        }

        _currentWeight = 0f;
        ApplyCarrySpeedModifier();

        _carriedItem.transform.SetParent(null);

        if (_carriedItem.TryGetComponent(out Rigidbody dropRb))
        {
            dropRb.isKinematic = false;
            dropRb.detectCollisions = true;
            dropRb.linearVelocity = Vector3.zero;
            dropRb.angularVelocity = Vector3.zero;

            Collider playerCol = GetComponent<Collider>();
            Collider itemCol = _carriedItem.GetComponent<Collider>();

            if (playerCol && itemCol)
                Physics.IgnoreCollision(playerCol, itemCol, true);

            Vector3 throwDir = (transform.forward + Vector3.up * Random.Range(1f, 2f)).normalized;
            dropRb.linearVelocity = worldVelocity;
            dropRb.AddForce(throwDir * Random.Range(6f, 10f), ForceMode.Impulse);
            dropRb.AddTorque(Random.insideUnitSphere * Random.Range(2f, 5f), ForceMode.Impulse);

            if (playerCol && itemCol)
                StartCoroutine(ReenableCollision(playerCol, itemCol, 0.5f));
        }

        _carriedItem = null;
        _carriedItemNetworkId = null;
        carriedObject = null;
        _currentState = PlayerState.Grounded;

        AudioController.Instance.PlaySound(
    AudioType.DropItem,
    AudioSourceType.Player,
    transform.position
);
    }

    public void PickUpItem(GameObject item) => PickUpItemServerRPC(item);
    public void DropItem() => DropItemServerRPC();

    private void CheckPickupRangeFeedback()
    {
        if (!_pickupOrigin) _pickupOrigin = transform;

        bool found = false;
        if (Physics.Raycast(_pickupOrigin.position, _pickupOrigin.forward, out RaycastHit hit, _pickupRange, _itemLayer, QueryTriggerInteraction.Ignore))
        {
            ItemPickUp itemPickUp = hit.collider.GetComponentInParent<ItemPickUp>();
            found = itemPickUp != null;
        }

        _isInPickupRange = found;
        if (_pickupFeedbackInstance) _pickupFeedbackInstance.SetActive(_isInPickupRange);
    }

    private void UpdateCarryingFeedback()
    {
        if (_carryingFeedbackInstance) _carryingFeedbackInstance.SetActive(_carriedItem != null);
    }

    private IEnumerator FindCanvasWithRetry()
    {
        for (int i = 0; i < 50; i++)
        {
            var go = GameObject.FindWithTag("Canvas");
            var canvas = go != null ? go.GetComponent<Canvas>() : FindObjectOfType<Canvas>();
            if (canvas != null && canvas.gameObject.activeInHierarchy)
            {
                _mainCanvas = canvas;
                SetupFeedbacks();
                yield break;
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void SetupFeedbacks()
    {
        if (_mainCanvas == null) return;

        if (pickupFeedbackPrefab)
        {
            _pickupFeedbackInstance = Instantiate(pickupFeedbackPrefab, _mainCanvas.transform);
            _pickupFeedbackInstance.SetActive(false);
        }

        if (carryingFeedbackPrefab)
        {
            _carryingFeedbackInstance = Instantiate(carryingFeedbackPrefab, _mainCanvas.transform);
            _carryingFeedbackInstance.SetActive(false);
        }
    }

    private void ApplyCarrySpeedModifier()
    {
        float weightFactor = Mathf.Clamp01(_currentWeight / 10f);
        walkingSpeed = _baseWalkSpeed * (1f - weightFactor * _maxCarryWeightSlowdown);
    }

    private IEnumerator ReenableCollision(Collider a, Collider b, float delay)
    {
        yield return new WaitForSeconds(delay);
        Physics.IgnoreCollision(a, b, false);
    }

    public void ApplyExternalForce(Vector3 force) => worldVelocity += force;
    public Vector3 GetGroundNormal() => groundNormal;
    public bool IsGrounded => isGrounded;
    public Vector3 GroundNormal => groundNormal;
    public void AddVelocity(Vector3 vel) => worldVelocity += vel;
    public Collider GetCarriedCollider() => carriedObject != null ? carriedObject.GetComponent<Collider>() : null;
}