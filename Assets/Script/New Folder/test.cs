
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class MoleCharacterMono : MonoBehaviour
{

    [Header("Movement")]
    public float walkingSpeed = 7.5f;
    public float jumpVelocity = 6.5f;
    public float snapDownDist = 1.0f;
    public float skinWidth = 0.01f;
    public int maxBounces = 6;
    public float maxPushSpeed = 5.0f;
    public LayerMask collisionMask = ~0;

    [Header("Sphere Collider Shape")]
    public Vector3 colliderCenter = Vector3.zero;
    public float colliderRadius = 0.5f;

    [Header("Camera")]
    public Transform cameraRig;         

    [Header("Input (New Input System)")]
    public InputActionReference moveAction;
    public InputActionReference jumpAction;

    [Header("Rotation smoothing")]
    [Range(1f, 30f)] public float rotationSmoothing = 10f;


    private Rigidbody rb;
    private SphereCollider sc;

    private Vector3 groundNormal;

    private Vector3 worldVelocity;        
    private bool isGrounded;
    private bool wasGrounded;
    private Vector3 groundSurfaceNormal;

    private bool jumpBuffered;
    private float jumpBufferTimer;
    private const float JumpBufferTime = 0.05f;
    private const float JumpCooldown = 0.25f;
    private float jumpCooldownTimer;

    private Vector3 previousPosition;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sc = GetComponent<SphereCollider>();

        rb.isKinematic = true;
        rb.useGravity = false;

        sc.center = colliderCenter;
        sc.radius = colliderRadius;

        groundNormal = -Physics.gravity.normalized;
        previousPosition = transform.position;
    }

    void OnEnable()
    {
        moveAction?.action.Enable();
        jumpAction?.action.Enable();
       // jumpAction?.action.performed += OnJump;
    }

    void OnDisable()
    {
        //jumpAction?.action.performed -= OnJump;
    }

    void OnJump(InputAction.CallbackContext ctx)
    {
        if (jumpCooldownTimer <= 0f)
        {
            jumpBuffered = true;
            jumpBufferTimer = JumpBufferTime;
        }
    }

    void Update()
    {
        jumpBufferTimer -= Time.deltaTime;
        jumpCooldownTimer -= Time.deltaTime;
        if (jumpBufferTimer < 0) jumpBuffered = false;
    }

    void FixedUpdate()
    {
        rb.isKinematic = true;

        Vector2 raw = moveAction?.action.ReadValue<Vector2>() ?? Vector2.zero;
        Vector3 inputDir = new Vector3(raw.x, 0, raw.y);

  
        Vector3 desired = ComputeDesiredMovement(inputDir) * Time.fixedDeltaTime;

  
        if (jumpBuffered && isGrounded)
        {
            worldVelocity = groundNormal * jumpVelocity;
            jumpBuffered = false;
            jumpCooldownTimer = JumpCooldown;
        }


        PerformMovement(desired);


        if (!isGrounded)
        {
            worldVelocity += Physics.gravity * Time.fixedDeltaTime;
        }
        else
        {
     
            float normalDot = Vector3.Dot(worldVelocity, groundNormal);
            if (normalDot < 0)
                worldVelocity -= normalDot * groundNormal;  
        }

        if (worldVelocity.magnitude > 0.001f)
        {
            PerformMovement(worldVelocity * Time.fixedDeltaTime);
        }


        PushOutOverlapping();


        wasGrounded = isGrounded;
        CheckGrounded();

        UpdateRotation();

        previousPosition = transform.position;
        SnapDown();
    }


    Vector3 ComputeDesiredMovement(Vector3 inputDir)
    {
        if (inputDir.magnitude < 1e-4f) return Vector3.zero;

        float yaw = cameraRig != null ? cameraRig.eulerAngles.y : 0f;

        float dot = Vector3.Dot(Vector3.Project(groundNormal, Vector3.up), Vector3.up);
        Vector3 worldUp = (dot >= 0f) ? Vector3.up : Vector3.down;
        if (Mathf.Approximately(dot, -1f)) yaw = -yaw;

        var planeRotation = Quaternion.FromToRotation(worldUp, groundNormal);
        var playerRotation = Quaternion.AngleAxis(yaw, worldUp);
        Vector3 forward = planeRotation * playerRotation * Vector3.forward;
        var moveRotation = Quaternion.LookRotation(forward, groundNormal);

        return walkingSpeed * (moveRotation * inputDir).normalized;
    }


    void PerformMovement(Vector3 movement)
    {
        float remainingDist = movement.magnitude;
        Vector3 dir = movement.normalized;
        Vector3 momentum = movement;

        for (int i = 0; i < maxBounces && remainingDist > 1e-5f; i++)
        {
            (Vector3 wCenter, float wRadius) = GetWorldSphere(-skinWidth);
            int hits = Physics.SphereCastNonAlloc(
                wCenter, wRadius, dir,
                hitCache, remainingDist + skinWidth,
                collisionMask, QueryTriggerInteraction.Ignore);
            RaycastHit best = default;
            float bestDist = float.MaxValue;
            bool found = false;
            for (int h = 0; h < hits; h++)
            {
                var hit = hitCache[h];
                if (hit.collider.transform == transform) continue;
                float d = Mathf.Max(hit.distance - skinWidth, 0f);
                if (d < bestDist) { bestDist = d; best = hit; best.distance = d; found = true; }
            }

            if (!found)
            {
                transform.position += dir * remainingDist;
                remainingDist = 0;
                break;
            }

            transform.position += dir * best.distance;
            remainingDist -= best.distance;

            Vector3 normal = best.normal;
            if (normal == Vector3.zero) normal = groundNormal;

            bool perpendicular = Vector3.Dot(normal, momentum) <= 1e-5f;
            if (perpendicular)
            {

                momentum = groundNormal * remainingDist;
            }
            else
            {
                momentum = Quaternion.LookRotation(normal) * momentum.normalized * remainingDist;
            }

            groundNormal = normal;
            dir = momentum.normalized;
            remainingDist = momentum.magnitude;
        }
    }

    void CheckGrounded()
    {
        (Vector3 wCenter, float wRadius) = GetWorldSphere(skinWidth * 0.5f);
        Vector3 castDir = -groundNormal;
        float castDist = skinWidth * 4f;

        int hits = Physics.SphereCastNonAlloc(
            wCenter, wRadius, castDir, hitCache, castDist,
            collisionMask, QueryTriggerInteraction.Ignore);

        isGrounded = false;
        groundSurfaceNormal = groundNormal;

        for (int i = 0; i < hits; i++)
        {
            if (hitCache[i].collider.transform == transform) continue;
            groundSurfaceNormal = hitCache[i].normal;
            isGrounded = true;
            break;
        }

        if (isGrounded)
        {
            groundNormal = groundSurfaceNormal;
        }
        else if (groundNormal == Vector3.zero)
        {
            groundNormal = -Physics.gravity.normalized;
        }
    }

    void SnapDown()
    {
        (Vector3 wCenter, float wRadius) = GetWorldSphere(-skinWidth);
        int hits = Physics.SphereCastNonAlloc(
            wCenter, wRadius, -groundNormal, hitCache,
            snapDownDist + skinWidth, collisionMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits; i++)
        {
            var hit = hitCache[i];
            if (hit.collider.transform == transform) continue;
            float snapDist = Mathf.Max(hit.distance - skinWidth, 0f);
            transform.position += -groundNormal * snapDist;
            break;
        }
    }


    void PushOutOverlapping()
    {
        (Vector3 wCenter, float wRadius) = GetWorldSphere(-skinWidth);
        int count = Physics.OverlapSphereNonAlloc(wCenter, wRadius, overlapCache, collisionMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            var col = overlapCache[i];
            if (col.transform == transform) continue;

            if (Physics.ComputePenetration(
                sc, transform.position, transform.rotation,
                col, col.transform.position, col.transform.rotation,
                out Vector3 dir, out float dist))
            {
                Vector3 push = dir * Mathf.Min(dist, maxPushSpeed * Time.deltaTime);
                transform.position += push;
            }
        }
    }

    private void UpdateRotation()
    {
        // Direction du mouvement (sur le plan de la surface)
        Vector3 planarVelocity = Vector3.ProjectOnPlane(worldVelocity, groundNormal);

        // Si on bouge, on oriente vers la direction du mouvement
        if (planarVelocity.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(planarVelocity.normalized, groundNormal);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * rotationSmoothing);
        }
        else
        {
            // Sinon on garde juste l'alignement avec la surface
            Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, groundNormal);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * rotationSmoothing);
        }
    }

    (Vector3 center, float radius) GetWorldSphere(float radiusMod = 0f)
    {
        Vector3 c = transform.rotation * colliderCenter + transform.position;
        float r = colliderRadius + radiusMod;
        return (c, r);
    }

    private readonly RaycastHit[] hitCache = new RaycastHit[32];
    private readonly Collider[] overlapCache = new Collider[32];

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        (Vector3 c, float r) = GetWorldSphere();
        Gizmos.color = isGrounded ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(c, r);
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, groundNormal);
    }
#endif

    public bool IsGrounded => isGrounded;

    public Vector3 GroundNormal => groundNormal;

    public void AddVelocity(Vector3 vel) => worldVelocity += vel;
}