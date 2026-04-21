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

    [Header("Dynamic Zoom")]
    public float minDistance = 2.5f;
    public float maxDistance = 7f;
    public float zoomSmoothSpeed = 3f;

    private float yaw;
    private float pitch;
    private float currentDistance;

    public void Setup(Transform newTarget, RoachController1 controller)
    {
        target = newTarget;
        roachController = controller;

        if (target == null) return;

        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
        currentDistance = distance;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        float targetDistance = distance;
        yaw += mouseDelta.x * mouseSensitivity;
        if (roachController != null)
        {
            Vector3 groundNormal = roachController.GetGroundNormal();
            float dot = Mathf.Clamp01(Vector3.Dot(groundNormal, Vector3.up));
            float t = dot * dot;
            targetDistance = Mathf.Lerp(maxDistance, minDistance, t);
        }

        currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * zoomSmoothSpeed);

        Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 targetPosition = target.position
                               + Vector3.up * height
                               - targetRotation * Vector3.forward * currentDistance;

        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSmoothSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothSpeed);
    }
}