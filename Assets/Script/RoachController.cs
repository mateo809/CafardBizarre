using PurrNet;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class RoachController : NetworkBehaviour
{
    public enum PlayerState { Grounded, Jumping, Falling, Gliding, WallClimbing, Carrying }

    // ─── Movement ────────────────────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float _walkSpeed = 5f;
    [SerializeField] private float _sprintMultiplier = 1.5f;
    [SerializeField] private float _airControlMultiplier = 0.6f;

    [Header("Gravity / Jump")]
    [SerializeField] private float _gravityStrength = 25f;
    [SerializeField] private float _glideGravity = 6f;
    [SerializeField] private float _glideFallRate = -1.5f;
    [SerializeField] private float _jumpForce = 6f;
    [SerializeField] private float _jumpCooldown = 0.35f;
    [SerializeField] private float _coyoteTime = 0.15f;
    [SerializeField] private float _jumpBufferTime = 0.2f;

    // ─── Collision (Mole SphereCast) ─────────────────────────────────────────
    [Header("Collision (Mole Sphere)")]
    public LayerMask collisionMask;
    [SerializeField] private float _skinWidth = 0.02f;
    [SerializeField] private int _maxBounces = 6;
    [SerializeField] private float _maxSlopeAngle = 80f;
    [SerializeField] private float _snapDownDist = 0.3f;

    // ─── Rotation ─────────────────────────────────────────────────────────────
    [Header("Rotation")]
    [SerializeField] private float _rotationSpeed = 12f;
    [SerializeField] private float _strafeRotationSpeed = 8f;
    [SerializeField] private float _alignSpeed = 8f;

    // ─── Camera ───────────────────────────────────────────────────────────────
    [Header("Camera")]
    [SerializeField] private Transform _cameraPivot;
    [SerializeField] private float _cameraDistance = 5f;
    [SerializeField] private float _cameraHeight = 2f;
    [SerializeField] private float _cameraFollowSpeed = 10f;
    [SerializeField] private float _cameraRotationSpeed = 8f;
    [SerializeField] private Vector2 _cameraSensitivity = new Vector2(2f, 2f);
    [SerializeField] private Vector2 _cameraPitchLimits = new Vector2(-30f, 60f);

    // ─── Carrying ─────────────────────────────────────────────────────────────
    [Header("Carrying")]
    [SerializeField] private float _maxCarryWeightSlowdown = 0.5f;
    [SerializeField] private Transform _pickupOrigin;
    [SerializeField] private Transform _transportPoint;
    [SerializeField] private float _pickupRange = 3f;
    [SerializeField] private LayerMask _itemLayer = ~0;

    // ─── Network Animation ────────────────────────────────────────────────────
    [Header("Network Animation")]
    [SerializeField] private NetworkAnimator _networkAnimator;

    // ─── UI Feedback ──────────────────────────────────────────────────────────
    [Header("UI Feedback")]
    [SerializeField] private GameObject pickupFeedbackPrefab;
    [SerializeField] private GameObject carryingFeedbackPrefab;

    // ─── Private state ────────────────────────────────────────────────────────
    private Rigidbody _rb;
    private SphereCollider _sc;

    private Vector3 _velocity = Vector3.zero;
    private Vector3 _groundNormal = Vector3.up;

    private bool _grounded;
    private bool _wasGrounded;

    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _jumpPressed;
    private bool _jumpHeld;
    private bool _sprintHeld;

    private float _nextJumpTime;
    private float _lastGroundedTime;
    private float _jumpBufferTimer;
    private float _cameraYaw;
    private float _cameraPitch;

    private GameObject _carriedItem;
    private NetworkIdentity _carriedItemNetworkId;
    private bool _isCarryingObject;
    private float _currentWeight;
    private float _baseWalkSpeed;

    // Forward desire memorise pour la rotation au repos
    private Vector3 _desiredForward = Vector3.forward;

    private GameObject _pickupFeedbackInstance;
    private GameObject _carryingFeedbackInstance;
    private Canvas _mainCanvas;
    private bool _isInPickupRange;

    private readonly RaycastHit[] _hitCache = new RaycastHit[16];

    public PlayerState _currentState = PlayerState.Grounded;
    public GameObject carriedObject;

    // =========================================================================
    //  LIFECYCLE
    // =========================================================================

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _sc = GetComponent<SphereCollider>();
        _rb.isKinematic = true;
        _rb.useGravity = false;
        _groundNormal = Vector3.up;
        _desiredForward = transform.forward;
    }

    private void Start()
    {
        _baseWalkSpeed = _walkSpeed;
        if (isOwner)
            StartCoroutine(FindCanvasWithRetry());
    }

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

    private void Update()
    {
        if (_jumpPressed) _jumpBufferTimer = _jumpBufferTime;
        else _jumpBufferTimer -= Time.deltaTime;

        if (_grounded) _lastGroundedTime = Time.time;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // 1. Sol
        CheckGround();

        // 2. Gravite / saut
        HandleGravityAndJump();

        // 3. Mouvement input
        Vector3 desiredMove = GetDesiredMove();
        if (desiredMove.sqrMagnitude > 0.0001f)
        {
            _desiredForward = desiredMove.normalized;
            MoveWithBounces(desiredMove * dt);
        }

        // 4. Velocite (gravite / jump)
        if (_velocity.sqrMagnitude > 0.0001f)
            MoveWithBounces(_velocity * dt);

        // 5. Snap sol
        if (_wasGrounded && _grounded && Time.time >= _nextJumpTime)
            TrySnapToSurface();

        // 6. Re-check sol + annulation velocite
        _wasGrounded = _grounded;
        CheckGround();

        if (_grounded)
        {
            float pen = Vector3.Dot(_velocity, _groundNormal);
            if (pen < 0f) _velocity -= pen * _groundNormal;
        }

        // 7. Etat / rotation / animations / feedback
        UpdateState();
        RotateCharacter();
        UpdateAnimations();
        CheckPickupRangeFeedback();
        UpdateCarryingFeedback();
    }

    private void LateUpdate()
    {
        HandleCameraMovement();
    }

    // =========================================================================
    //  MOLE SPHERE ENGINE
    // =========================================================================

    private void CheckGround()
    {
        Vector3 origin = GetSphereOrigin();

        int hits = Physics.SphereCastNonAlloc(
            origin, _sc.radius, -_groundNormal,
            _hitCache, _skinWidth * 4f,
            collisionMask, QueryTriggerInteraction.Ignore);

        _grounded = false;

        for (int i = 0; i < hits; i++)
        {
            ref RaycastHit h = ref _hitCache[i];
            if (h.collider.transform == transform) continue;
            _groundNormal = h.normal;
            _grounded = true;
            break;
        }
    }

    private void MoveWithBounces(Vector3 movement)
    {
        for (int i = 0; i < _maxBounces; i++)
        {
            float dist = movement.magnitude;
            if (dist < 0.0001f) break;

            Vector3 dir = movement / dist;
            Vector3 origin = GetSphereOrigin();

            int hits = Physics.SphereCastNonAlloc(
                origin, _sc.radius, dir,
                _hitCache, dist + _skinWidth,
                collisionMask, QueryTriggerInteraction.Ignore);

            float bestDist = float.MaxValue;
            Vector3 bestNormal = Vector3.zero;
            bool found = false;

            for (int h = 0; h < hits; h++)
            {
                ref RaycastHit hit = ref _hitCache[h];
                if (hit.collider.transform == transform) continue;
                float d = Mathf.Max(hit.distance - _skinWidth, 0f);
                if (d < bestDist) { bestDist = d; bestNormal = hit.normal; found = true; }
            }

            if (!found)
            {
                transform.position += movement;
                return;
            }

            transform.position += dir * bestDist;
            float remaining = dist - bestDist;

            // FIX flottement Y : ProjectOnPlane au lieu de Quaternion.LookRotation
            movement = Vector3.ProjectOnPlane(dir * remaining, bestNormal);
            _groundNormal = bestNormal;
        }
    }

    private void TrySnapToSurface()
    {
        Vector3 origin = GetSphereOrigin();

        int hits = Physics.SphereCastNonAlloc(
            origin, _sc.radius, -_groundNormal,
            _hitCache, _snapDownDist + _skinWidth,
            collisionMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits; i++)
        {
            ref RaycastHit hit = ref _hitCache[i];
            if (hit.collider.transform == transform) continue;

            float snapDist = Mathf.Max(hit.distance - _skinWidth, 0f);
            if (snapDist > 0f) transform.position -= _groundNormal * snapDist;

            _groundNormal = hit.normal;
            _grounded = true;

            float pen = Vector3.Dot(_velocity, _groundNormal);
            if (pen < 0f) _velocity -= pen * _groundNormal;
            break;
        }
    }

    private Vector3 GetSphereOrigin()
        => transform.position + transform.rotation * _sc.center;

    // =========================================================================
    //  GRAVITY & JUMP
    // =========================================================================

    private void HandleGravityAndJump()
    {
        if (!_grounded)
        {
            float g = (_currentState == PlayerState.Gliding) ? _glideGravity : _gravityStrength;
            _velocity += -_groundNormal * g * Time.fixedDeltaTime;
            _groundNormal = Vector3.Slerp(_groundNormal, Vector3.up, Time.fixedDeltaTime * 5f);
        }
        else
        {
            _velocity = Vector3.ProjectOnPlane(_velocity, _groundNormal);
        }

        if (_currentState == PlayerState.Gliding)
        {
            float fall = Vector3.Dot(_velocity, -_groundNormal);
            if (fall < _glideFallRate)
                _velocity += _groundNormal * (fall - _glideFallRate);
        }

        bool canJump = _grounded || (Time.time - _lastGroundedTime) <= _coyoteTime;

        if (_jumpBufferTimer > 0f && canJump && Time.time >= _nextJumpTime && _carriedItem == null)
        {
            _grounded = false;
            _velocity = _groundNormal * _jumpForce;
            _jumpBufferTimer = 0f;
            _nextJumpTime = Time.time + _jumpCooldown;
        }

        if (_jumpPressed && _currentState == PlayerState.WallClimbing && _carriedItem == null)
        {
            Vector3 pushDir = (_groundNormal + Vector3.up * 0.5f).normalized;
            _velocity = pushDir * _jumpForce;
            _nextJumpTime = Time.time + _jumpCooldown;
        }

        _jumpPressed = false;
    }

    // =========================================================================
    //  MOVEMENT DIRECTION
    // =========================================================================

    private Vector3 GetDesiredMove()
    {
        if (_moveInput.sqrMagnitude < 0.01f) return Vector3.zero;

        Transform cam = _cameraPivot ? _cameraPivot : Camera.main?.transform;
        if (!cam) return Vector3.zero;

        Vector3 camF = Vector3.ProjectOnPlane(cam.forward, _groundNormal).normalized;
        Vector3 camR = Vector3.ProjectOnPlane(cam.right, _groundNormal).normalized;
        Vector3 move = camF * _moveInput.y + camR * _moveInput.x;

        float speed = _walkSpeed;
        if (_sprintHeld && _grounded && _carriedItem == null) speed *= _sprintMultiplier;
        if (!_grounded) speed *= _airControlMultiplier;

        return move * speed;
    }

    // =========================================================================
    //  STATE
    // =========================================================================

    private void UpdateState()
    {
        if (_isCarryingObject) { _currentState = PlayerState.Carrying; return; }

        if (_grounded)
        {
            _currentState = PlayerState.Grounded;
        }
        else
        {
            float angle = Vector3.Angle(_groundNormal, Vector3.up);
            if (angle > _maxSlopeAngle) _currentState = PlayerState.WallClimbing;
            else if (_jumpHeld) _currentState = PlayerState.Gliding;
            else _currentState = PlayerState.Falling;
        }
    }

    // =========================================================================
    //  ROTATION
    // =========================================================================

    private void RotateCharacter()
    {
        Vector3 up = _groundNormal;
        float dt = Time.fixedDeltaTime;

        if (_moveInput.sqrMagnitude > 0.01f)
        {
            // Forward projete sur la surface, base orthonormale garantie
            Vector3 forward = Vector3.ProjectOnPlane(_desiredForward, up);
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.ProjectOnPlane(transform.forward, up);
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            forward.Normalize();

            Vector3 right = Vector3.Cross(up, forward).normalized;
            forward = Vector3.Cross(right, up).normalized;

            float speed = (_moveInput.x != 0 && Mathf.Abs(_moveInput.y) < 0.5f)
                ? _strafeRotationSpeed : _rotationSpeed;

            Quaternion target = Quaternion.LookRotation(forward, up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, speed * dt);
        }
        else
        {
            // Au repos : aligner uniquement le up, conserver le forward actuel
            Quaternion align = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, align, _alignSpeed * dt);
        }
    }

    // =========================================================================
    //  CAMERA
    // =========================================================================

    private void HandleCameraMovement()
    {
        if (!_cameraPivot) return;

        _cameraYaw += _lookInput.x * _cameraSensitivity.x;
        _cameraPitch -= _lookInput.y * _cameraSensitivity.y;
        _cameraPitch = Mathf.Clamp(_cameraPitch, _cameraPitchLimits.x, _cameraPitchLimits.y);

        Quaternion targetRot = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
        _cameraPivot.rotation = Quaternion.Slerp(
            _cameraPivot.rotation, targetRot, _cameraRotationSpeed * Time.deltaTime);

        Vector3 targetPos = transform.position
            - _cameraPivot.forward * _cameraDistance
            + _groundNormal * _cameraHeight;

        _cameraPivot.position = Vector3.Lerp(
            _cameraPivot.position, targetPos, _cameraFollowSpeed * Time.deltaTime);
    }

    // =========================================================================
    //  INPUT CALLBACKS
    // =========================================================================

    public void OnMove(InputAction.CallbackContext ctx) => _moveInput = ctx.ReadValue<Vector2>();
    public void OnLook(InputAction.CallbackContext ctx) => _lookInput = ctx.ReadValue<Vector2>();

    public void OnSprint(InputAction.CallbackContext ctx)
    {
        if (ctx.started) _sprintHeld = true;
        if (ctx.canceled) _sprintHeld = false;
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.started) { _jumpPressed = true; _jumpHeld = true; }
        if (ctx.canceled) _jumpHeld = false;
    }

    public void OnPickUp(InputAction.CallbackContext ctx)
    {
        if (!ctx.started || _carriedItem != null) return;
        if (!_pickupOrigin) _pickupOrigin = transform;

        Ray ray = new Ray(_pickupOrigin.position, _pickupOrigin.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, _pickupRange, _itemLayer))
        {
            int mask = 1 << hit.collider.gameObject.layer;
            if ((_itemLayer.value & mask) != 0)
                PickUpItemServerRPC(hit.collider.gameObject);
        }
    }

    public void OnDrop(InputAction.CallbackContext ctx)
    {
        if (!ctx.started || _carriedItem == null) return;
        DropItemServerRPC();
    }

    // =========================================================================
    //  CARRY (Network)
    // =========================================================================

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
            rb.linearVelocity = _velocity;
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

    // =========================================================================
    //  HELPERS
    // =========================================================================

    private void ApplyCarrySpeedModifier()
    {
        float weightFactor = Mathf.Clamp01(_currentWeight / 10f);
        _walkSpeed = _baseWalkSpeed * (1f - weightFactor * _maxCarryWeightSlowdown);
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
        _networkAnimator.SetBool("Run",
            running || (_currentState == PlayerState.WallClimbing
                     && _moveInput.sqrMagnitude > 0.01f && !carrying));
    }

    private void CheckPickupRangeFeedback()
    {
        if (!_pickupOrigin) _pickupOrigin = transform;
        _isInPickupRange = Physics.Raycast(
            _pickupOrigin.position, _pickupOrigin.forward, _pickupRange, _itemLayer);
        if (_pickupFeedbackInstance)
            _pickupFeedbackInstance.SetActive(_isInPickupRange);
    }

    private void UpdateCarryingFeedback()
    {
        if (_carryingFeedbackInstance)
            _carryingFeedbackInstance.SetActive(_carriedItem != null);
    }

    private IEnumerator ReenableCollision(Collider a, Collider b, float delay)
    {
        yield return new WaitForSeconds(delay);
        Physics.IgnoreCollision(a, b, false);
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
        Debug.LogError("[RoachController] Canvas introuvable apres 5 secondes !");
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _grounded ? Color.green : Color.yellow;
        if (_sc) Gizmos.DrawWireSphere(transform.position, _sc.radius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, _groundNormal * 0.8f);
        if (_pickupOrigin)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(
                _pickupOrigin.position,
                _pickupOrigin.position + _pickupOrigin.forward * _pickupRange);
        }
    }

    // ─── Accesseurs publics ──────────────────────────────────────────────────
    public bool IsGrounded => _grounded;
    public Vector3 GroundNormal => _groundNormal;
    public void AddVelocity(Vector3 vel) => _velocity += vel;
    public Collider GetCarriedCollider()
        => carriedObject != null ? carriedObject.GetComponent<Collider>() : null;
}