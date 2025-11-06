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
    [SerializeField] private float _airControlMultiplier = 0.6f;
    [SerializeField] private float _acceleration = 20f;

    [Header("Rotation")]
    [SerializeField] private float _rotationSpeed = 10f;
    [SerializeField] private float _rotationInputSpeed = 180f;

    [Header("Jump / Glide")]
    [SerializeField] private float _jumpForce = 6f;
    [SerializeField] private float _gravityStrength = 25f;
    [SerializeField] private float _glideGravity = 6f;
    [SerializeField] private float _glideFallRate = -1.5f;
    [SerializeField] private float _jumpCooldown = 0.35f;

    [Header("Wall Crawl")]
    [SerializeField] private float _wallCheckDistance = 1f;
    [SerializeField] private float _alignSpeed = 8f;
    [SerializeField] private float _maxWallAngle = 130f;
    [SerializeField] private LayerMask _climbableLayers = ~0;

    [Header("Ground Check")]
    [SerializeField] private Transform _groundCheck;
    [SerializeField] private float _groundCheckDistance = 0.5f;
    [SerializeField] private LayerMask _groundLayers = ~0;

    [Header("Camera")]
    [SerializeField] private Transform _cameraPivot;

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

    // --- UI Feedback ajout ---
    [Header("UI Feedback")]
    [SerializeField] private GameObject pickupFeedbackPrefab;   // Feedback quand proche d’un objet
    [SerializeField] private GameObject carryingFeedbackPrefab; // Feedback quand on porte un objet

    private GameObject pickupFeedbackInstance;
    private GameObject carryingFeedbackInstance;
    private Canvas mainCanvas;
    private bool isInPickupRange = false;
    // --------------------------

    private Rigidbody _rb;
    private Vector2 _moveInput;
    private bool _jumpPressed;
    private bool _jumpHeld;
    private float _nextJumpTime;

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

        if (!_cameraPivot)
        {
            GameObject camObj = new GameObject("CameraPivot");
            camObj.transform.SetParent(transform);
            camObj.transform.localPosition = Vector3.zero;
            _cameraPivot = camObj.transform;
        }

        if (_networkAnimator)
            _networkAnimator.applyRootMotion = false;
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
        // -------------------------------------
    }

    private void FixedUpdate()
    {
        DetectWallOrFloor();
        UpdateState();
        HandleOrientation();
        HandleManualRotation();
        HandleCameraPivot();

        Vector3 desiredVelocity = CalculateDesiredVelocity();
        ApplyMovement(desiredVelocity);
        ApplyGravityAndJump();

        UpdateAnimations();

        CheckPickupRangeFeedback();
        UpdateCarryingFeedback();

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

    private void HandleManualRotation()
    {
        float yRotation = _moveInput.x * _rotationInputSpeed * Time.fixedDeltaTime;
        transform.Rotate(Vector3.up, yRotation, Space.Self);
    }

    private Vector3 CalculateDesiredVelocity()
    {
        Vector3 moveDir = (transform.forward * _moveInput.y) + (transform.right * _moveInput.x);
        moveDir = moveDir.normalized;

        float speed = _walkSpeed * ((_currentState == PlayerState.Grounded || _currentState == PlayerState.WallClimbing) ? 1f : _airControlMultiplier);

        return moveDir * speed;
    }

    private void ApplyMovement(Vector3 desiredVelocity)
    {
        Vector3 currentVel = _rb.linearVelocity;
        Vector3 horizontalVel = Vector3.ProjectOnPlane(currentVel, _currentSurfaceNormal);

        float lerpFactor = (_currentState == PlayerState.Grounded) ? 0.15f : 0.1f;
        Vector3 smoothedVel = Vector3.Lerp(horizontalVel, desiredVelocity, lerpFactor * _acceleration * Time.fixedDeltaTime);

        _rb.linearVelocity = smoothedVel + _currentSurfaceNormal * Vector3.Dot(currentVel, _currentSurfaceNormal);
    }


    private void ApplyGravityAndJump()
    {
        Vector3 gravityDir = -_currentSurfaceNormal;
        float g = (_currentState == PlayerState.Gliding) ? _glideGravity : _gravityStrength;
        _rb.AddForce(gravityDir * g, ForceMode.Acceleration);

        if (_jumpPressed && Time.time >= _nextJumpTime && _carriedItem == null)
        {
            if (_currentState == PlayerState.Grounded)
            {
                _rb.linearVelocity = transform.up * _jumpForce;
                _currentState = PlayerState.Jumping;
            }
            else if (_currentState == PlayerState.WallClimbing)
            {
                _currentState = PlayerState.Falling;
                Vector3 pushDir = (_currentSurfaceNormal + Vector3.up * 0.5f).normalized;
                _rb.linearVelocity = pushDir * _jumpForce;
            }

            _jumpPressed = false;
            _nextJumpTime = Time.time + _jumpCooldown;
        }

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
            case PlayerState.Carrying: if (!grounded) _currentState = PlayerState.Falling; break;
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

    private void HandleCameraPivot()
    {
        if (!_cameraPivot) return;

        Quaternion targetRot = Quaternion.FromToRotation(_cameraPivot.up, _currentSurfaceNormal) * _cameraPivot.rotation;
        _cameraPivot.rotation = Quaternion.Slerp(_cameraPivot.rotation, targetRot, _rotationSpeed * Time.fixedDeltaTime);

        Vector3 desiredPos = transform.position - _cameraPivot.forward * 5f + _currentSurfaceNormal * 2f;
        _cameraPivot.position = Vector3.Lerp(_cameraPivot.position, desiredPos, Time.fixedDeltaTime * 5f);
    }

    public void OnMove(InputAction.CallbackContext ctx)
    {
        _moveInput = ctx.ReadValue<Vector2>();
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
