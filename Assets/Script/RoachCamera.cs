using UnityEngine;
using UnityEngine.InputSystem;

public class RoachCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    public Transform Target;
    public float Distance = 5f;
    public float Height = 2f;
    public float MouseSensitivity = 2f;
    public float RotationSmoothTime = 0.1f;
    public float MinPitch = -30f;
    public float MaxPitch = 60f;
    public LayerMask CollisionLayers = ~0;

    private float _yaw;
    private float _pitch;
    private Vector3 _currentRotation;
    private Vector3 _rotationSmoothVelocity;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Vector3 angles = transform.eulerAngles;
        _yaw = angles.y;
        _pitch = angles.x;
    }

    public void OnLook(InputAction.CallbackContext ctx)
    {
        Vector2 look = ctx.ReadValue<Vector2>();
        _yaw += look.x * MouseSensitivity;
        _pitch -= look.y * MouseSensitivity;
        _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
    }

    private void LateUpdate()
    {
        if (!Target) return;

        Vector3 targetRotation = new Vector3(_pitch, _yaw);
        _currentRotation = Vector3.SmoothDamp(_currentRotation, targetRotation, ref _rotationSmoothVelocity, RotationSmoothTime);

        Quaternion rot = Quaternion.Euler(_currentRotation.x, _currentRotation.y, 0f);

        Vector3 desiredPosition = Target.position - rot * Vector3.forward * Distance + Vector3.up * Height;

        RaycastHit hit;
        if (Physics.Linecast(Target.position + Vector3.up * Height, desiredPosition, out hit, CollisionLayers))
        {
            desiredPosition = hit.point + (Target.position - hit.point).normalized * 0.3f;
        }

        transform.position = desiredPosition;
        transform.LookAt(Target.position + Vector3.up * Height);
    }
}
