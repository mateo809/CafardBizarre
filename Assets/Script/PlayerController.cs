using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
public class RoachController : MonoBehaviour
{
    [Header("Déplacement")]
    public float groundSpeed = 5f;
    public float wallSpeed = 3f;
    public float turnSpeed = 8f;

    [Header("Jump & Hover ")]
    public float jumpForce = 6f;
    public float glideGravityScale = 0.3f;
    public float jumpCooldown = 0.5f; // en secondes

    [Header("Adhesion & Surfaces")]
    public float stickForce = 50f;
    public float surfaceCheckDistance = 1.5f;
    public LayerMask climbableLayers;
    public float minClimbSpeed = 1.2f;
    public float gravityCompensationFactor = 0.6f; // fraction de gravité compensée

    [Header("Weight & Inventory")]
    public float currentHoldingWeight = 0;
    public float maxHoldingWeight = 50;
    private bool _isInventoryOpen = false;
    public int inventoryMaxSize = 4;
    private PlayerInventory _inventory;
    private InteractionConroller _interactionConroller;


    [Header("Camera")]
    public Transform cameraPivot;
    public float cameraFollowSpeed = 6f;

    private Rigidbody rb;
    private PlayerInput playerInput;

    private Vector2 moveInput;
    private Vector3 surfaceNormal = Vector3.up;
    private bool isClimbing = false;
    private bool isGrounded = false;
    private bool isGliding = false;
    private bool isSliding = false;
    private bool jumpHeld = false;
    private float lastJumpTime = -10f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        playerInput = GetComponent<PlayerInput>();
        _inventory = GetComponent<PlayerInventory>();
        _inventory.SetInventoryVisibilityAtFalse();
    }

    // ==========================
    // INPUT SYSTEM CALLBACKS
    // ==========================
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            jumpHeld = true;
            TryJump();
        }
        else if (context.canceled)
        {
            jumpHeld = false;
            isGliding = false;
        }
    }

    public void OnInventoryOpening(InputAction.CallbackContext context)
    {
        if (context.started )
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

 

    // ==========================
    // UPDATE
    // ==========================
    void Update()
    {
        UpdateSurfaceNormal();
        UpdateCameraFollow();
        UpdateGlideState();
    }

    void FixedUpdate()
    {
        StickToSurface();
        MoveAndRotate();
        HandleWallClimbAndSlide();
    }

    // ==========================
    // SURFACE / ADHÉRENCE
    // ==========================
    void UpdateSurfaceNormal()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, -transform.up, out hit, surfaceCheckDistance, climbableLayers))
        {
            surfaceNormal = hit.normal;
            isGrounded = true;
            isClimbing = Vector3.Dot(surfaceNormal, Vector3.up) < 0.9f;
        }
        else if (Physics.Raycast(transform.position, transform.forward, out hit, surfaceCheckDistance, climbableLayers))
        {
            surfaceNormal = hit.normal;
            isGrounded = true;
            isClimbing = true;
        }
        else
        {
            surfaceNormal = Vector3.up;
            isGrounded = false;
            isClimbing = false;
        }
    }

    void StickToSurface()
    {
        if (isGrounded)
        {
            Quaternion targetRot = Quaternion.FromToRotation(transform.up, surfaceNormal) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.fixedDeltaTime);
            rb.AddForce(-transform.up * stickForce, ForceMode.Acceleration);
        }
    }

    // ==========================
    // DEPLACEMENT & ROTATION
    // ==========================
    void MoveAndRotate()
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal).normalized;
        Vector3 right = Vector3.Cross(surfaceNormal, forward);
        Vector3 moveDir = (forward * moveInput.y + right * moveInput.x).normalized;

        float currentSpeed = isClimbing ? wallSpeed : groundSpeed;
        Vector3 desiredVelocity = moveDir * currentSpeed;
        Vector3 newVelocity = new Vector3(desiredVelocity.x, rb.linearVelocity.y, desiredVelocity.z);
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, newVelocity, 0.25f);

        if (moveDir.magnitude > 0.1f && moveInput.y >= 0f)
        {
            Quaternion lookRot = Quaternion.LookRotation(moveDir, surfaceNormal);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, turnSpeed * Time.fixedDeltaTime);
        }
    }

    void HandleWallClimbAndSlide()
    {
        if (isClimbing)
        {
            // Compense la gravité partiellement
            Vector3 gravityComp = surfaceNormal * Physics.gravity.magnitude * gravityCompensationFactor;
            rb.AddForce(gravityComp, ForceMode.Acceleration);

            // Force tangentielle pour monter
            if (moveInput.y > 0.1f)
            {
                Vector3 climbDir = Vector3.Cross(transform.right, surfaceNormal).normalized;
                rb.AddForce(climbDir * wallSpeed * 1.5f, ForceMode.Acceleration);
            }

            float climbSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

            if (Mathf.Abs(climbSpeed) < minClimbSpeed && !isSliding)
                StartSlide();
            else if (Mathf.Abs(climbSpeed) >= minClimbSpeed && isSliding)
                StopSlide();

            if (isSliding)
            {
                Vector3 slideDir = Vector3.Cross(surfaceNormal, transform.right).normalized;
                rb.AddForce(slideDir * Physics.gravity.magnitude * 0.5f, ForceMode.Acceleration);
            }
        }
        else
        {
            StopSlide();
        }
    }

    void StartSlide()
    {
        isSliding = true;
        rb.linearDamping = 0.5f;
    }

    void StopSlide()
    {
        isSliding = false;
        rb.linearDamping = 0f;
    }

    void TryJump()
    {
        if (Time.time - lastJumpTime < jumpCooldown) return;

        if (isGrounded || isClimbing)
        {
            Vector3 jumpDir = isClimbing ? surfaceNormal : Vector3.up;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z); // reset vertical
            rb.AddForce(jumpDir * jumpForce, ForceMode.VelocityChange);

            isGrounded = false;
            isClimbing = false;
            lastJumpTime = Time.time;
        }
    }

    void UpdateGlideState()
    {
        if (!isGrounded && jumpHeld && rb.linearVelocity.y < 0f)
        {
            if (!isGliding)
                isGliding = true;

            rb.AddForce(-Physics.gravity * (1f - glideGravityScale), ForceMode.Acceleration);
            rb.linearDamping = glideGravityScale * 2f;
        }
        else if (isGliding)
        {
            isGliding = false;
            rb.linearDamping = 0f;
        }
    }

    void UpdateCameraFollow()
    {
        if (!cameraPivot) return;
        Quaternion targetRot = Quaternion.LookRotation(transform.forward, transform.up);
        cameraPivot.rotation = Quaternion.Slerp(cameraPivot.rotation, targetRot, cameraFollowSpeed * Time.deltaTime);
        cameraPivot.position = Vector3.Lerp(cameraPivot.position, transform.position, cameraFollowSpeed * Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position - transform.up * surfaceCheckDistance);
    }



}
