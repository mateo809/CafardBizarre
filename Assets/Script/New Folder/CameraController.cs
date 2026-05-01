using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public RoachController1 roachController;

    [Header("Camera Settings")]
    public float distance = 4f;
    public float height = 1.5f;
    public float mouseSensitivity = 2f;
    public float rotationSmoothSpeed = 12f;
    public float followSmoothSpeed = 10f;

    [Header("Pitch Limits")]
    public float minPitch = -20f;
    public float maxPitch = 60f;

    [Header("Dynamic Zoom (slope)")]
    public float minDistance = 2.5f;
    public float maxDistance = 7f;
    public float zoomSmoothSpeed = 3f;

    [Header("Wall Collision")]
    [SerializeField] private float _cameraRadius = 0.25f;
    [SerializeField] private float _wallZoomSpeed = 15f;
    [SerializeField] private LayerMask _collisionMask = ~0;
    // Minimum distance enforced when a wall is hit
    [SerializeField] private float _minWallDistance = 0.8f;

    private float yaw;
    private float pitch;
    private float currentDistance;

    // Desired distance before wall correction (set by slope zoom)
    private float _targetDistance;

    public void Setup(Transform newTarget, RoachController1 controller)
    {
        target = newTarget;
        roachController = controller;
        if (target == null) return;

        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
        currentDistance = distance;
        _targetDistance = distance;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // ?? Mouse input ??????????????????????????????????????????????????????
        Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // ?? Slope-based target distance ??????????????????????????????????????
        float slopeTarget = distance;
        if (roachController != null)
        {
            Vector3 groundNormal = roachController.GetGroundNormal();
            float dot = Mathf.Clamp01(Vector3.Dot(groundNormal, Vector3.up));
            float t = dot * dot;
            slopeTarget = Mathf.Lerp(maxDistance, minDistance, t);
        }

        _targetDistance = Mathf.Lerp(_targetDistance, slopeTarget, Time.deltaTime * zoomSmoothSpeed);

        // ?? Compute desired camera position ??????????????????????????????????
        Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivotPos = target.position + Vector3.up * height;
        Vector3 desiredDir = -(targetRotation * Vector3.forward);   // direction FROM pivot TO camera

        // ?? Wall collision: SphereCast from pivot toward camera ???????????????
        float allowedDistance = _targetDistance;

        if (Physics.SphereCast(
                pivotPos,
                _cameraRadius,
                desiredDir,
                out RaycastHit hit,
                _targetDistance,
                _collisionMask,
                QueryTriggerInteraction.Ignore))
        {
            // Pull camera in front of the wall, with a small offset so it doesn't clip
            allowedDistance = Mathf.Max(hit.distance - _cameraRadius * 0.5f, _minWallDistance);
        }

        // Snap inward instantly when a wall is hit; ease back out smoothly
        if (allowedDistance < currentDistance)
            currentDistance = Mathf.Lerp(currentDistance, allowedDistance, Time.deltaTime * _wallZoomSpeed);
        else
            currentDistance = Mathf.Lerp(currentDistance, allowedDistance, Time.deltaTime * zoomSmoothSpeed);

        // ?? Final position & rotation ?????????????????????????????????????????
        Vector3 targetPosition = pivotPos + desiredDir * currentDistance;

        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSmoothSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothSpeed);
    }
}