using PurrNet;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class RoachController1 : NetworkBehaviour
{
    public enum PlayerState { Grounded, Jumping, Falling, Gliding, WallClimbing, Carrying }
    public PlayerState _currentState = PlayerState.Grounded;

    [Header("Movement Settings")]
    public float walkingSpeed = 7.5f;
    public float jumpVelocity = 6.5f;
    public float snapDownDist = 1.0f;
    public float skinWidth = 0.03f;
    public int maxBounces = 6;
    public LayerMask collisionMask = ~0;

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

    [Header("Anti-Stuck")]
    [SerializeField] private float _stuckCheckInterval = 0.4f;
    [SerializeField] private float _stuckDistThreshold = 0.05f;
    [SerializeField] private float _stuckEjectForce = 6f;
    [SerializeField] private int _stuckMaxAttempts = 3;

    private Rigidbody rb;
    private SphereCollider sc;

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

    private BackendCaller _backendCaller;

    protected override void OnSpawned()
    {
        base.OnSpawned();
        if (!isOwner) return;

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

    void Awake()
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

    void OnEnable()
    {
        moveAction?.action.Enable();
        jumpAction?.action.Enable();
        sprintAction?.action.Enable();
        pickUpAction?.action.Enable();
        dropAction?.action.Enable();
    }

    void OnDisable()
    {
    }

    private void Update()
    {
        if (!isOwner) return;

        _moveInput = moveAction?.action.ReadValue<Vector2>() ?? Vector2.zero;

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

    private void OnPickUpPressed()
    {
        if (_carriedItem != null) return;
        if (_pickupOrigin == null) _pickupOrigin = transform;

        Vector3 origin = _pickupOrigin.position;
        Vector3 direction = _pickupOrigin.forward;

        Debug.DrawRay(origin, direction * _pickupRange, Color.red, 1f);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, _pickupRange, _itemLayer, QueryTriggerInteraction.Ignore))
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

    void FixedUpdate()
    {
        if (!isOwner) return;

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

        UpdateState();
        UpdateRotation();
        UpdateAnimations();
        CheckPickupRangeFeedback();
        UpdateCarryingFeedback();

        _jumpPressed = false;
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

        Vector3[] probes = {
            Vector3.up, Vector3.down,
            Vector3.left, Vector3.right,
            Vector3.forward, Vector3.back
        };

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
        else
        {
            float pen = Vector3.Dot(worldVelocity, groundNormal);
            if (pen < 0f) worldVelocity -= pen * groundNormal;
        }

        if (_currentState == PlayerState.Gliding)
        {
            float fall = Vector3.Dot(worldVelocity, Vector3.down);
            if (fall > -_glideFallRate)
                worldVelocity.y = Mathf.MoveTowards(worldVelocity.y, _glideFallRate, Time.fixedDeltaTime * 5f);
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

        if (_jumpPressed && _currentState == PlayerState.WallClimbing && _carriedItem == null)
        {
            Vector3 pushDir = (groundNormal + Vector3.up * 0.5f).normalized;
            worldVelocity = pushDir * (jumpVelocity * _jumpPowerMultiplier);
            _nextJumpTime = Time.time + _jumpCooldown;
        }

        if (!_jumpHeld && worldVelocity.y > 0f)
            worldVelocity.y = Mathf.MoveTowards(worldVelocity.y, 0f, _gravityStrength * 2.5f * Time.fixedDeltaTime);
    }

    private void UpdateState()
    {
        if (_isCarryingObject) { _currentState = PlayerState.Carrying; return; }

        if (isGrounded)
            _currentState = PlayerState.Grounded;
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

        bool carrying = _carriedItem != null;
        _networkAnimator.SetBool("Item", carrying);

        bool flying = _currentState == PlayerState.Jumping
                   || _currentState == PlayerState.Falling
                   || _currentState == PlayerState.Gliding;

        bool running = _currentState == PlayerState.Grounded
                    && _moveInput.sqrMagnitude > 0.01f && !carrying;

        _networkAnimator.SetBool("Fly", flying);
        _networkAnimator.SetBool("Run", running || (_currentState == PlayerState.WallClimbing && _moveInput.sqrMagnitude > 0.01f && !carrying));
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

        // ── 1. Disable physics BEFORE reparenting ──────────────────────────
        if (item.TryGetComponent(out Rigidbody itemRb))
        {
            itemRb.isKinematic = true;
            itemRb.detectCollisions = false;
            itemRb.linearVelocity = Vector3.zero;
            itemRb.angularVelocity = Vector3.zero;
        }

        // ── 2. Notify ItemPickUp component ─────────────────────────────────
        if (item.TryGetComponent(out ItemPickUp itemPickUp))
        {
            itemPickUp.Interact(gameObject);
            _currentWeight = itemPickUp.weight;
        }

        // ── 3. Choose anchor – fallback to player root if not assigned ─────
        Transform anchor = (_transportPoint != null) ? _transportPoint : transform;

        // ── 4. Reparent (worldPositionStays = false → local space reset) ───
        item.transform.SetParent(anchor, false);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;
        item.transform.localScale = Vector3.one;

        //// ── 5. Elevate above anchor using InChildren so nested Renderers work
        //Renderer rend = item.GetComponentInChildren<Renderer>();
        //if (rend != null)
        //{
        //    // Half-height offset so item sits on top of the transport point
        //    float halfH = rend.bounds.extents.y;
        //    item.transform.localPosition = new Vector3(0f, halfH + 0.05f, 0f);
        //}

        // ── 6. Register carried state ──────────────────────────────────────
        carriedObject = item;
        _carriedItem = item;
        _isCarryingObject = true;
        _currentState = PlayerState.Carrying;
        _carriedItemNetworkId = item.GetComponent<NetworkIdentity>();

        ApplyCarrySpeedModifier();
    }

    [ServerRpc]
    private void DropItemServerRPC() => DropItemObserverRPC();

    [ObserversRpc]
    private void DropItemObserverRPC()
    {
        if (_carriedItem == null) return;

        _isCarryingObject = false;

        if (TryGetComponent(out InventoryManager inventory))
        {
            inventory.RemoveItem(_carriedItem.GetComponent<ItemPickUp>().itemData, 1);
            _carriedItem.GetComponent<ItemPickUp>().owner = null;
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
    }

    public void PickUpItem(GameObject item) => PickUpItemServerRPC(item);
    public void DropItem() => DropItemServerRPC();

    private void CheckPickupRangeFeedback()
    {
        if (!_pickupOrigin) _pickupOrigin = transform;
        _isInPickupRange = Physics.Raycast(_pickupOrigin.position, _pickupOrigin.forward, _pickupRange, _itemLayer);

        if (_pickupFeedbackInstance)
            _pickupFeedbackInstance.SetActive(_isInPickupRange);
    }

    private void UpdateCarryingFeedback()
    {
        if (_carryingFeedbackInstance)
            _carryingFeedbackInstance.SetActive(_carriedItem != null);
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isGrounded ? Color.green : Color.yellow;
        if (sc) Gizmos.DrawWireSphere(transform.position, sc.radius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, groundNormal * 0.8f);

        if (_pickupOrigin)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(_pickupOrigin.position, _pickupOrigin.position + _pickupOrigin.forward * _pickupRange);
        }

        // Visualise le transport point dans l'éditeur
        if (_transportPoint)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(_transportPoint.position, 0.15f);
        }
    }

    public void ApplyExternalForce(Vector3 force) => worldVelocity += force;
    public Vector3 GetGroundNormal() => groundNormal;
    public bool IsGrounded => isGrounded;
    public Vector3 GroundNormal => groundNormal;
    public void AddVelocity(Vector3 vel) => worldVelocity += vel;
    public Collider GetCarriedCollider()
        => carriedObject != null ? carriedObject.GetComponent<Collider>() : null;
}