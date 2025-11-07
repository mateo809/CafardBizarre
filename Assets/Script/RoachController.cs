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
    [SerializeField] private float _alignSpeed = 8f;
    [SerializeField] private float _maxWallAngle = 130f;
    [SerializeField] private LayerMask _climbableLayers = ~0;
    [SerializeField] private float _wallRotationSpeed = 5f;

    [Header("Ground Check")]
    [SerializeField] private Transform _groundCheck;
    [SerializeField] private float _groundCheckDistance = 0.5f;
    [SerializeField] private LayerMask _groundLayers = ~0;

    [Header("Network Animation")]
    [SerializeField] private NetworkAnimator _networkAnimator;

    [Header("Carrying Settings")]
    [SerializeField] private float _maxCarryWeightSlowdown = 0.5f;
    private float _currentWeight = 0f;
    private float _baseWalkSpeed;

    [Header("Item Pickup / Drop")]
    [SerializeField] private Transform _pickupOrigin;
    [SerializeField] private Transform _transportPoint;
    [SerializeField] private float _pickupRange = 3f;
    [SerializeField] private LayerMask _itemLayer = ~0;
    private GameObject _carriedItem;
    private bool _isCarryingObject = false;

    [Header("UI Feedback")]
    [SerializeField] private GameObject pickupFeedbackPrefab;
    [SerializeField] private GameObject carryingFeedbackPrefab;

    private GameObject pickupFeedbackInstance;
    private GameObject carryingFeedbackInstance;
    private Canvas mainCanvas;
    private bool isInPickupRange = false;

    // Core components
    private Rigidbody _rb;
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _jumpPressed;
    private bool _jumpHeld;
    private bool _sprintHeld;
    private float _nextJumpTime;
    private float _lastGroundedTime;
    private float _jumpBufferTimer;

    // Rotation smoothing
    private float _currentRotationVelocity;
    private float _targetRotation;
    private float _cameraYaw;
    private float _cameraPitch;

    public PlayerState _currentState = PlayerState.Grounded;
    private Vector3 _currentSurfaceNormal = Vector3.up;
    public GameObject carriedObject;

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

        // Créer le pivot de caméra si absent
        if (!_cameraPivot)
        {
            GameObject camObj = new GameObject("CameraPivot");
            camObj.transform.SetParent(null); // Pas de parent pour éviter les rotations parasites
            _cameraPivot = camObj.transform;
        }

        // Initialiser la rotation de caméra
        _cameraYaw = transform.eulerAngles.y;
        _cameraPitch = 0f;

        if (_networkAnimator)
            _networkAnimator.applyRootMotion = false;

        // Verrouiller le curseur pour un TPS
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (_groundCheck == null)
        {
            GameObject g = new GameObject("GroundCheck");
            g.transform.SetParent(transform);
            g.transform.localPosition = Vector3.zero;
            _groundCheck = g.transform;
        }
    }

    private void Start()
    {
        _baseWalkSpeed = _walkSpeed;

        mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null)
        {
            Debug.LogError("Aucun Canvas trouvé dans la scène !");
        }
        else
        {
            if (pickupFeedbackPrefab != null)
            {
                pickupFeedbackInstance = Instantiate(pickupFeedbackPrefab, mainCanvas.transform);
                pickupFeedbackInstance.SetActive(false);
            }
            if (carryingFeedbackPrefab != null)
            {
                carryingFeedbackInstance = Instantiate(carryingFeedbackPrefab, mainCanvas.transform);
                carryingFeedbackInstance.SetActive(false);
            }
        }
    }

    private void Update()
    {
        // Mise à jour des timers dans Update pour plus de précision
        if (_jumpPressed)
            _jumpBufferTimer = _jumpBufferTime;
        else
            _jumpBufferTimer -= Time.deltaTime;

        if (IsGroundedRaycast())
            _lastGroundedTime = Time.time;
    }

    private void FixedUpdate()
    {
        DetectWallOrFloor();
        UpdateState();

        Vector3 desiredVelocity = CalculateDesiredVelocity();
        ApplyMovement(desiredVelocity);

        HandleRotation();
        HandleOrientation();
        ApplyGravityAndJump();

        UpdateAnimations();
        CheckPickupRangeFeedback();
        UpdateCarryingFeedback();
    }

    private void LateUpdate()
    {
        // Mise à jour de la caméra après le mouvement
        HandleCameraMovement();
    }

    private void CheckPickupRangeFeedback()
    {
        if (_pickupOrigin == null) _pickupOrigin = transform;

        bool wasInRange = isInPickupRange;
        isInPickupRange = Physics.Raycast(_pickupOrigin.position, _pickupOrigin.forward, _pickupRange, _itemLayer);

        if (pickupFeedbackInstance != null)
        {
            if (isInPickupRange && !wasInRange)
                pickupFeedbackInstance.SetActive(true);
            else if (!isInPickupRange && wasInRange)
                pickupFeedbackInstance.SetActive(false);
        }
    }

    private void UpdateCarryingFeedback()
    {
        if (carryingFeedbackInstance == null) return;

        if (_carriedItem != null && !carryingFeedbackInstance.activeSelf)
            carryingFeedbackInstance.SetActive(true);
        else if (_carriedItem == null && carryingFeedbackInstance.activeSelf)
            carryingFeedbackInstance.SetActive(false);
    }

    private Vector3 CalculateDesiredVelocity()
    {
        if (_moveInput.sqrMagnitude < 0.01f)
            return Vector3.zero;

        Vector3 moveDir;

        // Sur un mur, utiliser l'axe Y pour monter/descendre et X pour les côtés
        if (_currentState == PlayerState.WallClimbing)
        {
            // Y input = monter/descendre le long du mur
            // X input = se déplacer latéralement sur le mur
            Vector3 wallUp = -Vector3.Cross(_currentSurfaceNormal, transform.right).normalized;
            Vector3 wallRight = -Vector3.Cross(wallUp, _currentSurfaceNormal).normalized;

            moveDir = (wallUp * _moveInput.y + wallRight * _moveInput.x).normalized;
        }
        else
        {
            // Calculer la direction relative à la caméra (comportement normal)
            Vector3 cameraForward = _cameraPivot.forward;
            Vector3 cameraRight = _cameraPivot.right;

            // Projeter sur le plan de la surface
            cameraForward = Vector3.ProjectOnPlane(cameraForward, _currentSurfaceNormal).normalized;
            cameraRight = Vector3.ProjectOnPlane(cameraRight, _currentSurfaceNormal).normalized;

            // Direction de mouvement relative à la caméra
            moveDir = (cameraForward * _moveInput.y + cameraRight * _moveInput.x).normalized;
        }

        // Vitesse avec sprint
        float speed = _walkSpeed;
        if (_sprintHeld && _currentState == PlayerState.Grounded && _carriedItem == null)
            speed *= _sprintMultiplier;

        // Réduction en l'air (mais pas sur le mur)
        if (_currentState != PlayerState.Grounded && _currentState != PlayerState.WallClimbing)
            speed *= _airControlMultiplier;

        return moveDir * speed;
    }

    private void ApplyMovement(Vector3 desiredVelocity)
    {
        Vector3 currentVel = _rb.linearVelocity;
        Vector3 horizontalVel = Vector3.ProjectOnPlane(currentVel, _currentSurfaceNormal);

        // Accélération/décélération dynamique
        float accel = desiredVelocity.sqrMagnitude > 0.01f ? _acceleration : _deceleration;

        float lerpFactor = (_currentState == PlayerState.Grounded) ? 0.15f : 0.1f;
        Vector3 smoothedVel = Vector3.Lerp(horizontalVel, desiredVelocity, lerpFactor * accel * Time.fixedDeltaTime);

        _rb.linearVelocity = smoothedVel + _currentSurfaceNormal * Vector3.Dot(currentVel, _currentSurfaceNormal);
    }

    private void HandleRotation()
    {
        if (_moveInput.sqrMagnitude < 0.01f) return;

        Vector3 targetDirection;

        if (_currentState == PlayerState.WallClimbing)
        {
            // Définir les axes sur le mur
            Vector3 wallUp = -Vector3.Cross(_currentSurfaceNormal, transform.right).normalized;
            Vector3 wallRight = -Vector3.Cross(wallUp, _currentSurfaceNormal).normalized;

            targetDirection = (wallUp * _moveInput.y + wallRight * _moveInput.x).normalized;

            if (targetDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection, _currentSurfaceNormal);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _wallRotationSpeed * Time.fixedDeltaTime);
            }
        }
        else
        {
            Vector3 cameraForward = Vector3.ProjectOnPlane(_cameraPivot.forward, _currentSurfaceNormal).normalized;
            Vector3 cameraRight = Vector3.ProjectOnPlane(_cameraPivot.right, _currentSurfaceNormal).normalized;

            targetDirection = (cameraForward * _moveInput.y + cameraRight * _moveInput.x).normalized;

            if (targetDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection, _currentSurfaceNormal);
                float rotSpeed = (_moveInput.x != 0 && Mathf.Abs(_moveInput.y) < 0.5f) ? _strafeRotationSpeed : _rotationSpeed;
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotSpeed * Time.fixedDeltaTime);
            }
        }
    }


    private void ApplyGravityAndJump()
    {
        Vector3 gravityDir = -_currentSurfaceNormal;
        float g = (_currentState == PlayerState.Gliding) ? _glideGravity : _gravityStrength;
        _rb.AddForce(gravityDir * g, ForceMode.Acceleration);

        // Coyote time et jump buffer
        bool canJump = (Time.time - _lastGroundedTime) <= _coyoteTime || _currentState == PlayerState.Grounded;

        if (_jumpBufferTimer > 0 && canJump && Time.time >= _nextJumpTime && _carriedItem == null)
        {
            if (_currentState == PlayerState.Grounded || (Time.time - _lastGroundedTime) <= _coyoteTime)
            {
                _rb.linearVelocity = transform.up * _jumpForce;
                _currentState = PlayerState.Jumping;
                _jumpBufferTimer = 0f;
                _nextJumpTime = Time.time + _jumpCooldown;
            }
        }
        else if (_jumpPressed && _currentState == PlayerState.WallClimbing && _carriedItem == null)
        {
            _currentState = PlayerState.Falling;
            Vector3 pushDir = (_currentSurfaceNormal + Vector3.up * 0.5f).normalized;
            _rb.linearVelocity = pushDir * _jumpForce;
            _nextJumpTime = Time.time + _jumpCooldown;
        }

        _jumpPressed = false;

        // Limiter la vitesse de chute en glide
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

    private void HandleCameraMovement()
    {
        if (!_cameraPivot) return;

        // Rotation de la caméra avec la souris
        _cameraYaw += _lookInput.x * _cameraSensitivity.x;
        _cameraPitch -= _lookInput.y * _cameraSensitivity.y;
        _cameraPitch = Mathf.Clamp(_cameraPitch, _cameraPitchLimits.x, _cameraPitchLimits.y);

        // Orientation de la caméra
        Quaternion targetCameraRotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
        _cameraPivot.rotation = Quaternion.Slerp(
            _cameraPivot.rotation,
            targetCameraRotation,
            _cameraRotationSpeed * Time.deltaTime
        );

        // Position de la caméra derrière le joueur
        Vector3 targetPosition = transform.position
            - _cameraPivot.forward * _cameraDistance
            + _currentSurfaceNormal * _cameraHeight;

        _cameraPivot.position = Vector3.Lerp(
            _cameraPivot.position,
            targetPosition,
            _cameraFollowSpeed * Time.deltaTime
        );
    }

    private void DetectWallOrFloor()
    {
        if (!_isCarryingObject)
        {
            RaycastHit hit;
            Vector3[] directions = { transform.forward, -transform.forward, transform.right, -transform.right };
            bool wallFound = false;

            foreach (var dir in directions)
            {
                if (Physics.Raycast(transform.position, dir, out hit, _wallCheckDistance, _climbableLayers))
                {
                    float angle = Vector3.Angle(hit.normal, Vector3.up);
                    if (angle > 10f && angle < _maxWallAngle)
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
                    if (Physics.Raycast(_groundCheck.position, -_currentSurfaceNormal, out hit, 1f, _climbableLayers))
                        _currentSurfaceNormal = hit.normal;
                    else
                    {
                        _currentState = PlayerState.Falling;
                        _currentSurfaceNormal = Vector3.up;
                    }
                }
                else
                {
                    if (Physics.Raycast(_groundCheck.position, Vector3.down, out hit, _groundCheckDistance, _groundLayers))
                        _currentSurfaceNormal = hit.normal;
                    else
                        _currentSurfaceNormal = Vector3.up;
                }
            }
        }
    }

    private void UpdateState()
    {
        bool wallDetected = (_currentState != PlayerState.WallClimbing) && IsWallDetected();
        bool grounded = (_currentState != PlayerState.WallClimbing) && IsGroundedRaycast();

        switch (_currentState)
        {
            case PlayerState.Grounded:
                if (!grounded) _currentState = PlayerState.Falling;
                else if (wallDetected && _carriedItem == null) _currentState = PlayerState.WallClimbing;
                break;
            case PlayerState.Carrying:
                if (!grounded) _currentState = PlayerState.Falling;
                break;
            case PlayerState.Jumping:
                if (_rb.linearVelocity.y < 0) _currentState = PlayerState.Falling;
                break;
            case PlayerState.Falling:
                if (grounded) _currentState = PlayerState.Grounded;
                else if (_jumpHeld) _currentState = PlayerState.Gliding;
                else if (wallDetected && _carriedItem == null) _currentState = PlayerState.WallClimbing;
                break;
            case PlayerState.Gliding:
                if (grounded) _currentState = PlayerState.Grounded;
                else if (!_jumpHeld) _currentState = PlayerState.Falling;
                break;
        }
    }

    private bool IsGroundedRaycast()
    {
        return Physics.Raycast(_groundCheck.position, Vector3.down, _groundCheckDistance, _groundLayers);
    }

    private bool IsWallDetected()
    {
        RaycastHit hit;
        Vector3[] directions = { transform.forward, -transform.forward, transform.right, -transform.right };
        foreach (var dir in directions)
        {
            if (Physics.Raycast(transform.position, dir, out hit, _wallCheckDistance, _climbableLayers))
                return true;
        }
        return false;
    }

    private void HandleOrientation()
    {
        Quaternion alignToSurface = Quaternion.FromToRotation(transform.up, _currentSurfaceNormal) * transform.rotation;

        Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, _currentSurfaceNormal).normalized;
        if (projectedForward.sqrMagnitude > 0.001f)
        {
            Quaternion lookForward = Quaternion.LookRotation(projectedForward, _currentSurfaceNormal);
            alignToSurface = Quaternion.Slerp(alignToSurface, lookForward, 0.5f);
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, alignToSurface, _alignSpeed * Time.fixedDeltaTime);
    }

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
        if (ctx.started)
            _sprintHeld = true;
        else if (ctx.canceled)
            _sprintHeld = false;
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

    public void OnPickUp(InputAction.CallbackContext ctx)
    {
        if (!ctx.started) return;

        if (_carriedItem != null)
        {
            Debug.Log("Already carrying something, cannot pick up another item.");
            return;
        }

        if (_pickupOrigin == null)
            _pickupOrigin = transform;

        Ray ray = new Ray(_pickupOrigin.position, _pickupOrigin.forward);
        Debug.DrawRay(ray.origin, ray.direction * _pickupRange, Color.green, 1f);

        if (Physics.Raycast(ray, out RaycastHit hit, _pickupRange, _itemLayer))
        {
            int hitLayerMask = 1 << hit.collider.gameObject.layer;

            if ((_itemLayer.value & hitLayerMask) != 0)
            {
                Debug.Log("Picked up item on layer: " + LayerMask.LayerToName(hit.collider.gameObject.layer));
                PickUpItem(hit.collider.gameObject);
            }
            else
            {
                Debug.Log("Hit object not on valid pickup layer: " + hit.collider.name);
            }
        }
        else
        {
            Debug.Log("No item hit by raycast.");
        }
    }

    public void OnDrop(InputAction.CallbackContext ctx)
    {
        if (!ctx.started) return;

        if (_carriedItem == null)
        {
            Debug.Log("Nothing to drop.");
            return;
        }

        Debug.Log("Dropped " + _carriedItem.name);
        DropItem();
    }

    public void PickUpItem(GameObject item)
    {
        if (item == null)
            return;
        carriedObject = item;
        _isCarryingObject = true;
        _carriedItem = item;

        if (item.TryGetComponent(out ItemPickUp itemPickUp))
        {
            itemPickUp.Interact(gameObject);
        }
        _currentWeight = itemPickUp.weight;
        ApplyCarrySpeedModifier();

        Renderer itemRenderer = _carriedItem.GetComponent<Renderer>();
        Vector3 offset = Vector3.zero;

        if (itemRenderer != null)
        {
            _currentState = PlayerState.Carrying;

            float yOffset = itemRenderer.bounds.extents.y + 0.5f;
            float zOffset = 0.2f;
            offset = new Vector3(0, yOffset, zOffset);
        }

        _carriedItem.transform.SetParent(_transportPoint);
        _carriedItem.transform.localPosition = offset;
        _carriedItem.transform.localRotation = Quaternion.identity;

        Rigidbody rb = _carriedItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
    }

    public void DropItem()
    {
        if (_carriedItem == null) return;
        _isCarryingObject = false;
        InventoryManager inventory = GetComponent<InventoryManager>();
        if (inventory != null)
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

            Collider playerCollider = GetComponent<Collider>();
            Collider itemCollider = _carriedItem.GetComponent<Collider>();
            if (playerCollider != null && itemCollider != null)
            {
                Physics.IgnoreCollision(playerCollider, itemCollider, true);
            }

            Rigidbody playerRb = GetComponent<Rigidbody>();
            Vector3 playerVelocity = playerRb != null ? playerRb.linearVelocity : Vector3.zero;

            Vector3 throwDir = (transform.forward + Vector3.up * Random.Range(1f, 2f)).normalized;
            float throwForce = Random.Range(6f, 10f);

            rb.linearVelocity = playerVelocity;
            rb.AddForce(throwDir * throwForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * Random.Range(2f, 5f), ForceMode.Impulse);

            if (playerCollider != null && itemCollider != null)
            {
                StartCoroutine(ReenableCollision(playerCollider, itemCollider, 0.5f));
            }
        }

        Debug.Log("Dropped " + _carriedItem.name);
        _carriedItem = null;
        _currentState = PlayerState.Grounded;
    }

    private IEnumerator ReenableCollision(Collider a, Collider b, float delay)
    {
        yield return new WaitForSeconds(delay);
        Physics.IgnoreCollision(a, b, false);
    }

    private void UpdateAnimations()
    {
        if (_networkAnimator == null) return;

        bool carryingItem = _carriedItem != null;
        _networkAnimator.SetBool("Item", carryingItem);

        if (_currentState == PlayerState.WallClimbing)
        {
            bool isRunningOnWall = _moveInput.sqrMagnitude > 0.01f && !carryingItem;
            _networkAnimator.SetBool("Run", isRunningOnWall);
            _networkAnimator.SetBool("Fly", false);
        }
        else
        {
            bool isFlying = _currentState == PlayerState.Jumping ||
                            _currentState == PlayerState.Falling ||
                            _currentState == PlayerState.Gliding;

            bool isRunning = _currentState == PlayerState.Grounded && _moveInput.sqrMagnitude > 0.01f && !carryingItem;

            _networkAnimator.SetBool("Fly", isFlying);
            _networkAnimator.SetBool("Run", isRunning);
        }
    }

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

        Gizmos.color = Color.green;
        if (_pickupOrigin)
            Gizmos.DrawLine(_pickupOrigin.position, _pickupOrigin.position + _pickupOrigin.forward * _pickupRange);
    }

    public Collider GetCarriedCollider()
    {
        if (carriedObject != null)
            return carriedObject.GetComponent<Collider>();
        return null;
    }

    private void ApplyCarrySpeedModifier()
    {
        float weightFactor = Mathf.Clamp01(_currentWeight / 10f);
        float slowdown = 1f - weightFactor * _maxCarryWeightSlowdown;

        _walkSpeed = _baseWalkSpeed * slowdown;
    }
}