using UnityEngine;
using UnityEngine.InputSystem;
using PurrNet;

[RequireComponent(typeof(Rigidbody))]
public class RoachController : NetworkBehaviour
{
    public enum PlayerState { Grounded, Jumping, Falling, Gliding, WallClimbing }

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

    [Header("Weight & Inventory")]
    [SerializeField] public float currentHoldingWeight = 0;
    [SerializeField] public float maxHoldingWeight = 50;
    [SerializeField] private bool _isInventoryOpen = false;
    [SerializeField] public int inventoryMaxSize = 4;
    [SerializeField] private PlayerInventory _inventory;
    [SerializeField] private InteractionConroller _interactionConroller;


    [Header("Camera")]
    [SerializeField] private Transform _cameraPivot;

    [Header("Network Animation")]
    [SerializeField] private NetworkAnimator _networkAnimator;

    private Rigidbody _rb;
    private Vector2 _moveInput;
    private bool _jumpPressed;
    private bool _jumpHeld;
    private float _nextJumpTime;

    private PlayerState _currentState = PlayerState.Grounded;
    private Vector3 _currentSurfaceNormal = Vector3.up;

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
        Vector3 vel = _rb.linearVelocity;

        Vector3 localVel = Vector3.ProjectOnPlane(vel, _currentSurfaceNormal);
        Vector3 targetVel = Vector3.MoveTowards(localVel, desiredVelocity, _acceleration * Time.fixedDeltaTime);

        _rb.linearVelocity = targetVel + _currentSurfaceNormal * Vector3.Dot(vel, _currentSurfaceNormal);
    }


    private void ApplyGravityAndJump()
    {
        Vector3 gravityDir = -_currentSurfaceNormal;
        float g = (_currentState == PlayerState.Gliding) ? _glideGravity : _gravityStrength;
        _rb.AddForce(gravityDir * g, ForceMode.Acceleration);

        if (_jumpPressed && Time.time >= _nextJumpTime)
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

    private void UpdateState()
    {
        bool wallDetected = (_currentState != PlayerState.WallClimbing) && IsWallDetected();
        bool grounded = (_currentState != PlayerState.WallClimbing) && IsGroundedRaycast();

        switch (_currentState)
        {
            case PlayerState.Grounded:
                if (!grounded) _currentState = PlayerState.Falling;
                else if (wallDetected) _currentState = PlayerState.WallClimbing;
                break;
            case PlayerState.Jumping:
                if (_rb.linearVelocity.y < 0) _currentState = PlayerState.Falling;
                break;
            case PlayerState.Falling:
                if (grounded) _currentState = PlayerState.Grounded;
                else if (_jumpHeld) _currentState = PlayerState.Gliding;
                else if (wallDetected) _currentState = PlayerState.WallClimbing;
                break;
            case PlayerState.Gliding:
                if (grounded) _currentState = PlayerState.Grounded;
                else if (!_jumpHeld) _currentState = PlayerState.Falling;
                break;
        }
    }

    private bool IsGroundedRaycast()
    {
        RaycastHit hit;
        return Physics.Raycast(_groundCheck.position, Vector3.down, out hit, _groundCheckDistance, _groundLayers);
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

    public void OnInventoryOpening(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            if (_isInventoryOpen)
            {
                _isInventoryOpen = false;
                _inventory.SetInventoryVisibilityAtFalse();

            }
            else
            {
                _isInventoryOpen = true;
                _inventory.SetInventoryVisibilityAtTrue();

            }

        }
    }

    private void UpdateAnimations()
    {
        if (_networkAnimator == null) return;

        if (_currentState == PlayerState.WallClimbing)
        {
            bool isRunningOnWall = _moveInput.sqrMagnitude > 0.01f;
            _networkAnimator.SetBool("Run", isRunningOnWall);
            _networkAnimator.SetBool("Fly", false);
        }
        else
        {
            bool isFlying = _currentState == PlayerState.Jumping ||
                            _currentState == PlayerState.Falling ||
                            _currentState == PlayerState.Gliding;

            bool isRunning = _currentState == PlayerState.Grounded && _moveInput.sqrMagnitude > 0.01f;

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
    }
}
