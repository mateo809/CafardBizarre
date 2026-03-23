using PurrNet;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class RoachController : NetworkBehaviour
{
    public enum PlayerState { Grounded, Jumping, Falling, Gliding, WallClimbing, Carrying }

    [Header("Movement")]
    [SerializeField] private float _walkSpeed = 5f;
    [SerializeField] private float _sprintMultiplier = 1.5f;
    [SerializeField] private float _airControlMultiplier = 0.6f;
    [SerializeField] private float _acceleration = 20f;
    [SerializeField] private float _deceleration = 15f;

    [Header("Rotation")]
    [SerializeField] private float _rotationSpeed = 12f;
    [SerializeField] private float _rotationSmoothTime = 0.12f;
    [SerializeField] private bool _rotateTowardsMovement = true;
    [SerializeField] private float _strafeRotationSpeed = 8f;
    [SerializeField] private float _wallRotationSpeed = 5f;
    [SerializeField] private float _alignSpeed = 8f;

    [Header("Camera")]
    [SerializeField] private Transform _cameraPivot;
    [SerializeField] private float _cameraDistance = 5f;
    [SerializeField] private float _cameraHeight = 2f;
    [SerializeField] private float _cameraFollowSpeed = 10f;
    [SerializeField] private float _cameraRotationSpeed = 8f;
    [SerializeField] private Vector2 _cameraSensitivity = new Vector2(2f, 2f);
    [SerializeField] private Vector2 _cameraPitchLimits = new Vector2(-30f, 60f);

    [Header("Jump / Glide")]
    [SerializeField] private float _jumpForce = 6f;
    [SerializeField] private float _gravityStrength = 25f;
    [SerializeField] private float _glideGravity = 6f;
    [SerializeField] private float _glideFallRate = -1.5f;
    [SerializeField] private float _jumpCooldown = 0.35f;
    [SerializeField] private float _coyoteTime = 0.15f;
    [SerializeField] private float _jumpBufferTime = 0.2f;

    [Header("Wall Crawl")]
    [SerializeField] private float _wallCheckDistance = 1f;
    [SerializeField] private float _maxWallAngle = 130f;
    [SerializeField] private float _minWallAngle = 10f;
    // FIX : délai avant de transitionner vers WallClimbing (évite les blocages sur arêtes)
    [SerializeField] private float _wallTransitionDelay = 0.15f;
    // FIX : dot product minimum — filtre les normales d'arête fuyantes
    [SerializeField] private float _wallNormalDotThreshold = 0.3f;

    [Header("Ground Check")]
    [SerializeField] private Transform _groundCheck;
    [SerializeField] private float _groundCheckDistance = 0.5f;

    [Header("Network Animation")]
    [SerializeField] private NetworkAnimator _networkAnimator;

    [Header("Carrying Settings")]
    [SerializeField] private float _maxCarryWeightSlowdown = 0.5f;
    private float _currentWeight;
    private float _baseWalkSpeed;

    [Header("Item Pickup / Drop")]
    [SerializeField] private Transform _pickupOrigin;
    [SerializeField] private Transform _transportPoint;
    [SerializeField] private float _pickupRange = 3f;
    [SerializeField] private LayerMask _itemLayer = ~0;
    private GameObject _carriedItem;
    private bool _isCarryingObject;
    private NetworkIdentity _carriedItemNetworkId;

    [Header("UI Feedback")]
    [SerializeField] private GameObject pickupFeedbackPrefab;
    [SerializeField] private GameObject carryingFeedbackPrefab;

    private GameObject pickupFeedbackInstance;
    private GameObject carryingFeedbackInstance;
    private Canvas mainCanvas;
    private bool isInPickupRange;

    // ── Core components ──────────────────────────────────────────────
    private Rigidbody _rb;

    // Inputs
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _jumpPressed;
    private bool _jumpHeld;
    private bool _sprintHeld;

    // Jump timers
    private float _nextJumpTime;
    private float _lastGroundedTime;
    private float _jumpBufferTimer;

    // Camera angles
    private float _cameraYaw;
    private float _cameraPitch;

    public PlayerState _currentState = PlayerState.Grounded;
    private Vector3 _currentSurfaceNormal = Vector3.up;

    // FIX : timer de transition Grounded → WallClimbing
    private float _wallTransitionTimer = 0f;

    public GameObject carriedObject;

    // ─────────────────────────────────────────────────────────────────
    //  Network spawn
    // ─────────────────────────────────────────────────────────────────
    protected override void OnSpawned()
    {
        base.OnSpawned();

        if (!isOwner)
        {
            enabled = false;
            if (_cameraPivot) Destroy(_cameraPivot.gameObject);
            return;
        }

        enabled = true;

        var playerInput = GetComponent<PlayerInput>();
        if (playerInput) playerInput.enabled = true;

        if (!_cameraPivot)
        {
            var camObj = new GameObject("CameraPivot");
            camObj.transform.SetParent(null);
            _cameraPivot = camObj.transform;
        }

        _cameraYaw = transform.eulerAngles.y;
        _cameraPitch = 0f;

        if (_networkAnimator)
            _networkAnimator.applyRootMotion = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ─────────────────────────────────────────────────────────────────
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (_groundCheck == null)
        {
            var g = new GameObject("GroundCheck");
            g.transform.SetParent(transform);
            g.transform.localPosition = Vector3.zero;
            _groundCheck = g.transform;
        }
    }

    private void Start()
    {
        _baseWalkSpeed = _walkSpeed;
        if (isOwner)
            StartCoroutine(FindCanvasWithRetry());
    }

    private void Update()
    {
        if (_jumpPressed)
            _jumpBufferTimer = _jumpBufferTime;
        else
            _jumpBufferTimer -= Time.deltaTime;

        if (IsGroundedRaycast())
            _lastGroundedTime = Time.time;
    }

    private void FixedUpdate()
    {
        DetectSurface();
        UpdateState();
        ApplyMovement(CalculateDesiredVelocity());
        HandleRotation();
        HandleOrientation();
        ApplyGravityAndJump();
        UpdateAnimations();
        CheckPickupRangeFeedback();
        UpdateCarryingFeedback();
    }

    private void LateUpdate()
    {
        HandleCameraMovement();
    }

    // ─────────────────────────────────────────────────────────────────
    //  Surface detection
    //  FIX : filtre les normales d'arête via dot product (_wallNormalDotThreshold)
    // ─────────────────────────────────────────────────────────────────
    private void DetectSurface()
    {
        if (_isCarryingObject) return;

        Vector3[] dirs = { transform.forward, -transform.forward, transform.right, -transform.right };
        bool wallFound = false;

        foreach (var dir in dirs)
        {
            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, _wallCheckDistance))
            {
                float angle = Vector3.Angle(hit.normal, Vector3.up);

                // FIX : la normale doit pointer vers le joueur (filtre les arêtes fuyantes)
                float dotBack = Vector3.Dot(hit.normal, -dir);
                if (dotBack < _wallNormalDotThreshold) continue;

                if (angle > _minWallAngle && angle < _maxWallAngle)
                {
                    _currentSurfaceNormal = hit.normal;
                    _currentState = PlayerState.WallClimbing;
                    wallFound = true;
                    break;
                }
            }
        }

        if (!wallFound)
        {
            if (_currentState == PlayerState.WallClimbing)
            {
                if (Physics.Raycast(_groundCheck.position, -_currentSurfaceNormal, out RaycastHit floorHit, 1f))
                    _currentSurfaceNormal = floorHit.normal;
                else
                {
                    _currentState = PlayerState.Falling;
                    _currentSurfaceNormal = Vector3.up;
                }
            }
            else
            {
                if (Physics.Raycast(_groundCheck.position, Vector3.down, out RaycastHit groundHit, _groundCheckDistance))
                    _currentSurfaceNormal = groundHit.normal;
                else
                    _currentSurfaceNormal = Vector3.up;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  State machine
    //  FIX : délai _wallTransitionDelay avant Grounded → WallClimbing
    // ─────────────────────────────────────────────────────────────────
    private void UpdateState()
    {
        bool grounded = (_currentState != PlayerState.WallClimbing) && IsGroundedRaycast();
        bool wallDetected = (_currentState != PlayerState.WallClimbing) && IsWallDetected();

        switch (_currentState)
        {
            case PlayerState.Grounded:
                if (!grounded)
                {
                    // FIX : on accumule le timer avant de tomber (anti-blocage sur microarête)
                    _wallTransitionTimer += Time.fixedDeltaTime;
                    if (_wallTransitionTimer > _wallTransitionDelay)
                    {
                        _wallTransitionTimer = 0f;
                        _currentState = PlayerState.Falling;
                    }
                }
                else
                {
                    _wallTransitionTimer = 0f;
                    if (wallDetected && _carriedItem == null)
                        _currentState = PlayerState.WallClimbing;
                }
                break;

            case PlayerState.Carrying:
                if (!grounded) _currentState = PlayerState.Falling;
                break;

            case PlayerState.Jumping:
                if (_rb.linearVelocity.y < 0) _currentState = PlayerState.Falling;
                break;

            case PlayerState.Falling:
                _wallTransitionTimer = 0f;
                if (grounded) _currentState = PlayerState.Grounded;
                else if (_jumpHeld) _currentState = PlayerState.Gliding;
                else if (wallDetected && _carriedItem == null) _currentState = PlayerState.WallClimbing;
                break;

            case PlayerState.Gliding:
                _wallTransitionTimer = 0f;
                if (grounded) _currentState = PlayerState.Grounded;
                else if (!_jumpHeld) _currentState = PlayerState.Falling;
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Movement
    // ─────────────────────────────────────────────────────────────────
    private Vector3 CalculateDesiredVelocity()
    {
        if (_moveInput.sqrMagnitude < 0.01f) return Vector3.zero;

        Vector3 moveDir;

        if (_currentState == PlayerState.WallClimbing)
        {
            Vector3 wallUp = -Vector3.Cross(_currentSurfaceNormal, transform.right).normalized;
            Vector3 wallRight = -Vector3.Cross(wallUp, _currentSurfaceNormal).normalized;
            moveDir = (wallUp * _moveInput.y + wallRight * _moveInput.x).normalized;
        }
        else
        {
            Vector3 camFwd = Vector3.ProjectOnPlane(_cameraPivot.forward, _currentSurfaceNormal).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(_cameraPivot.right, _currentSurfaceNormal).normalized;
            moveDir = (camFwd * _moveInput.y + camRight * _moveInput.x).normalized;
        }

        float speed = _walkSpeed;
        if (_sprintHeld && _currentState == PlayerState.Grounded && _carriedItem == null)
            speed *= _sprintMultiplier;
        if (_currentState != PlayerState.Grounded && _currentState != PlayerState.WallClimbing)
            speed *= _airControlMultiplier;

        return moveDir * speed;
    }

    private void ApplyMovement(Vector3 desiredVelocity)
    {
        Vector3 currentVel = _rb.linearVelocity;
        Vector3 horizontalVel = Vector3.ProjectOnPlane(currentVel, _currentSurfaceNormal);

        float lerpFactor = (_currentState == PlayerState.Grounded) ? 0.15f : 0.1f;
        float accel = desiredVelocity.sqrMagnitude > 0.01f ? _acceleration : _deceleration;

        Vector3 smoothedVel = Vector3.Lerp(horizontalVel, desiredVelocity, lerpFactor * accel * Time.fixedDeltaTime);
        _rb.linearVelocity = smoothedVel + _currentSurfaceNormal * Vector3.Dot(currentVel, _currentSurfaceNormal);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Rotation & orientation
    // ─────────────────────────────────────────────────────────────────
    private void HandleRotation()
    {
        if (_moveInput.sqrMagnitude < 0.01f) return;

        Vector3 targetDir;

        if (_currentState == PlayerState.WallClimbing)
        {
            Vector3 wallUp = -Vector3.Cross(_currentSurfaceNormal, transform.right).normalized;
            Vector3 wallRight = -Vector3.Cross(wallUp, _currentSurfaceNormal).normalized;
            targetDir = (wallUp * _moveInput.y + wallRight * _moveInput.x).normalized;

            if (targetDir.sqrMagnitude > 0.01f)
            {
                Quaternion rot = Quaternion.LookRotation(targetDir, _currentSurfaceNormal);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, _wallRotationSpeed * Time.fixedDeltaTime);
            }
        }
        else
        {
            Vector3 camFwd = Vector3.ProjectOnPlane(_cameraPivot.forward, _currentSurfaceNormal).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(_cameraPivot.right, _currentSurfaceNormal).normalized;
            targetDir = (camFwd * _moveInput.y + camRight * _moveInput.x).normalized;

            if (targetDir.sqrMagnitude > 0.01f)
            {
                Quaternion rot = Quaternion.LookRotation(targetDir, _currentSurfaceNormal);
                float rotSpeed = (_moveInput.x != 0 && Mathf.Abs(_moveInput.y) < 0.5f)
                                   ? _strafeRotationSpeed : _rotationSpeed;
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, rotSpeed * Time.fixedDeltaTime);
            }
        }
    }

    // FIX : alignSpeed dynamique — ralenti quand la normale change brusquement (arête)
    private void HandleOrientation()
    {
        float normalDelta = Vector3.Angle(transform.up, _currentSurfaceNormal);
        float dynamicAlignSpeed = normalDelta > 45f
            ? _alignSpeed * 0.3f   // transition douce sur arête
            : _alignSpeed;

        Quaternion alignToSurface = Quaternion.FromToRotation(transform.up, _currentSurfaceNormal) * transform.rotation;

        Vector3 projFwd = Vector3.ProjectOnPlane(transform.forward, _currentSurfaceNormal).normalized;
        if (projFwd.sqrMagnitude > 0.001f)
        {
            Quaternion lookFwd = Quaternion.LookRotation(projFwd, _currentSurfaceNormal);
            alignToSurface = Quaternion.Slerp(alignToSurface, lookFwd, 0.5f);
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, alignToSurface, dynamicAlignSpeed * Time.fixedDeltaTime);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Physics — gravité & saut
    // ─────────────────────────────────────────────────────────────────
    private void ApplyGravityAndJump()
    {
        Vector3 gravityDir = -_currentSurfaceNormal;
        float g = (_currentState == PlayerState.Gliding) ? _glideGravity : _gravityStrength;
        _rb.AddForce(gravityDir * g, ForceMode.Acceleration);

        bool canJump = (Time.time - _lastGroundedTime) <= _coyoteTime || _currentState == PlayerState.Grounded;

        if (_jumpBufferTimer > 0 && canJump && Time.time >= _nextJumpTime && _carriedItem == null)
        {
            _rb.linearVelocity = transform.up * _jumpForce;
            _currentState = PlayerState.Jumping;
            _jumpBufferTimer = 0f;
            _nextJumpTime = Time.time + _jumpCooldown;
        }
        else if (_jumpPressed && _currentState == PlayerState.WallClimbing && _carriedItem == null)
        {
            _currentState = PlayerState.Falling;
            Vector3 pushDir = (_currentSurfaceNormal + Vector3.up * 0.5f).normalized;
            _rb.linearVelocity = pushDir * _jumpForce;
            _nextJumpTime = Time.time + _jumpCooldown;
        }

        _jumpPressed = false;

        if (_currentState == PlayerState.Gliding)
        {
            Vector3 vel = _rb.linearVelocity;
            float fall = Vector3.Dot(vel, gravityDir);
            if (fall < _glideFallRate)
            {
                vel -= gravityDir * (fall - _glideFallRate);
                _rb.linearVelocity = vel;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Camera
    // ─────────────────────────────────────────────────────────────────
    private void HandleCameraMovement()
    {
        if (!_cameraPivot) return;

        _cameraYaw += _lookInput.x * _cameraSensitivity.x;
        _cameraPitch -= _lookInput.y * _cameraSensitivity.y;
        _cameraPitch = Mathf.Clamp(_cameraPitch, _cameraPitchLimits.x, _cameraPitchLimits.y);

        Quaternion targetRot = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
        _cameraPivot.rotation = Quaternion.Slerp(_cameraPivot.rotation, targetRot, _cameraRotationSpeed * Time.deltaTime);

        Vector3 targetPos = transform.position
            - _cameraPivot.forward * _cameraDistance
            + _currentSurfaceNormal * _cameraHeight;

        _cameraPivot.position = Vector3.Lerp(_cameraPivot.position, targetPos, _cameraFollowSpeed * Time.deltaTime);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Input callbacks
    // ─────────────────────────────────────────────────────────────────
    public void OnMove(InputAction.CallbackContext ctx)
    {
        _moveInput = ctx.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext ctx)
    {
        _lookInput = ctx.ReadValue<Vector2>();
    }

    public void OnSprint(InputAction.CallbackContext ctx)
    {
        if (ctx.started) _sprintHeld = true;
        else if (ctx.canceled) _sprintHeld = false;
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            _jumpPressed = true;
            _jumpHeld = true;
        }
        else if (ctx.canceled)
        {
            _jumpHeld = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Pickup / Drop
    // ─────────────────────────────────────────────────────────────────
    public void OnPickUp(InputAction.CallbackContext ctx)
    {
        if (!ctx.started) return;
        if (_carriedItem != null) return;
        if (_pickupOrigin == null) _pickupOrigin = transform;

        Ray ray = new Ray(_pickupOrigin.position, _pickupOrigin.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, _pickupRange, _itemLayer))
        {
            int hitLayerMask = 1 << hit.collider.gameObject.layer;
            if ((_itemLayer.value & hitLayerMask) != 0)
                PickUpItemServerRPC(hit.collider.gameObject);
        }
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

        carriedObject = item;
        _isCarryingObject = true;
        _carriedItem = item;

        if (item.TryGetComponent(out ItemPickUp itemPickUp))
        {
            itemPickUp.Interact(gameObject);
            _currentWeight = itemPickUp.weight;
        }

        ApplyCarrySpeedModifier();

        Vector3 offset = Vector3.zero;
        if (_carriedItem.TryGetComponent(out Renderer rend))
        {
            _currentState = PlayerState.Carrying;
            offset = new Vector3(0f, rend.bounds.extents.y + 0.5f, 0.2f);
        }

        _carriedItem.transform.SetParent(_transportPoint);
        _carriedItem.transform.localPosition = offset;
        _carriedItem.transform.localRotation = Quaternion.identity;

        if (_carriedItem.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        _carriedItemNetworkId = item.GetComponent<NetworkIdentity>();
    }

    public void OnDrop(InputAction.CallbackContext ctx)
    {
        if (!ctx.started || _carriedItem == null) return;
        DropItemServerRPC();
    }

    [ServerRpc] private void DropItemServerRPC() => DropItemObserverRPC();

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

        if (_carriedItem.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = false;
            rb.detectCollisions = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            Collider playerCol = GetComponent<Collider>();
            Collider itemCol = _carriedItem.GetComponent<Collider>();

            if (playerCol && itemCol)
                Physics.IgnoreCollision(playerCol, itemCol, true);

            Vector3 throwDir = (transform.forward + Vector3.up * Random.Range(1f, 2f)).normalized;
            Vector3 playerVel = TryGetComponent(out Rigidbody prb) ? prb.linearVelocity : Vector3.zero;

            rb.linearVelocity = playerVel;
            rb.AddForce(throwDir * Random.Range(6f, 10f), ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * Random.Range(2f, 5f), ForceMode.Impulse);

            if (playerCol && itemCol)
                StartCoroutine(ReenableCollision(playerCol, itemCol, 0.5f));
        }

        _carriedItem = null;
        _carriedItemNetworkId = null;
        _currentState = PlayerState.Grounded;
    }

    public void PickUpItem(GameObject item) => PickUpItemServerRPC(item);
    public void DropItem() => DropItemServerRPC();

    private IEnumerator ReenableCollision(Collider a, Collider b, float delay)
    {
        yield return new WaitForSeconds(delay);
        Physics.IgnoreCollision(a, b, false);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────
    private bool IsGroundedRaycast()
        => Physics.Raycast(_groundCheck.position, Vector3.down, _groundCheckDistance);

    private bool IsWallDetected()
    {
        Vector3[] dirs = { transform.forward, -transform.forward, transform.right, -transform.right };
        foreach (var dir in dirs)
        {
            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, _wallCheckDistance))
            {
                float angle = Vector3.Angle(hit.normal, Vector3.up);

                // FIX : même filtre dot product que dans DetectSurface
                float dotBack = Vector3.Dot(hit.normal, -dir);
                if (dotBack < _wallNormalDotThreshold) continue;

                if (angle > _minWallAngle && angle < _maxWallAngle)
                    return true;
            }
        }
        return false;
    }

    private void ApplyCarrySpeedModifier()
    {
        float weightFactor = Mathf.Clamp01(_currentWeight / 10f);
        _walkSpeed = _baseWalkSpeed * (1f - weightFactor * _maxCarryWeightSlowdown);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Animations
    // ─────────────────────────────────────────────────────────────────
    private void UpdateAnimations()
    {
        if (_networkAnimator == null) return;

        bool carrying = _carriedItem != null;
        _networkAnimator.SetBool("Item", carrying);

        if (_currentState == PlayerState.WallClimbing)
        {
            _networkAnimator.SetBool("Run", _moveInput.sqrMagnitude > 0.01f && !carrying);
            _networkAnimator.SetBool("Fly", false);
        }
        else
        {
            bool flying = _currentState == PlayerState.Jumping
                        || _currentState == PlayerState.Falling
                        || _currentState == PlayerState.Gliding;
            bool running = _currentState == PlayerState.Grounded
                        && _moveInput.sqrMagnitude > 0.01f && !carrying;

            _networkAnimator.SetBool("Fly", flying);
            _networkAnimator.SetBool("Run", running);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  UI Feedbacks
    // ─────────────────────────────────────────────────────────────────
    private void CheckPickupRangeFeedback()
    {
        if (_pickupOrigin == null) _pickupOrigin = transform;
        bool wasInRange = isInPickupRange;
        isInPickupRange = Physics.Raycast(_pickupOrigin.position, _pickupOrigin.forward, _pickupRange, _itemLayer);

        if (pickupFeedbackInstance != null)
            pickupFeedbackInstance.SetActive(isInPickupRange);
    }

    private void UpdateCarryingFeedback()
    {
        if (carryingFeedbackInstance == null) return;
        carryingFeedbackInstance.SetActive(_carriedItem != null);
    }

    private IEnumerator FindCanvasWithRetry()
    {
        for (int i = 0; i < 50; i++)
        {
            var go = GameObject.FindWithTag("Canvas");
            var canvas = go != null ? go.GetComponent<Canvas>() : FindObjectOfType<Canvas>();

            if (canvas != null && canvas.gameObject.activeInHierarchy)
            {
                mainCanvas = canvas;
                SetupFeedbacks();
                yield break;
            }
            yield return new WaitForSeconds(0.1f);
        }
        Debug.LogError("[RoachController] Canvas introuvable après 5 secondes !");
    }

    private void SetupFeedbacks()
    {
        if (mainCanvas == null) return;
        if (pickupFeedbackPrefab != null) { pickupFeedbackInstance = Instantiate(pickupFeedbackPrefab, mainCanvas.transform); pickupFeedbackInstance.SetActive(false); }
        if (carryingFeedbackPrefab != null) { carryingFeedbackInstance = Instantiate(carryingFeedbackPrefab, mainCanvas.transform); carryingFeedbackInstance.SetActive(false); }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Gizmos
    // ─────────────────────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (_groundCheck)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(_groundCheck.position, _groundCheck.position + Vector3.down * _groundCheckDistance);
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * _wallCheckDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position - transform.forward * _wallCheckDistance);
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, transform.position + transform.right * _wallCheckDistance);
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position - transform.right * _wallCheckDistance);

        if (_pickupOrigin)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(_pickupOrigin.position, _pickupOrigin.position + _pickupOrigin.forward * _pickupRange);
        }
    }

    public Collider GetCarriedCollider()
        => carriedObject != null ? carriedObject.GetComponent<Collider>() : null;
}