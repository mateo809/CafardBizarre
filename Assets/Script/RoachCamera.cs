using UnityEngine;
using UnityEngine.InputSystem;

public class RoachCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    public Transform Target;
    public float DefaultDistance = 5f;
    public float DefaultHeight = 2f;
    public float WallClimbDistance = 10f;
    public float WallClimbHeight = 0f;
    public float CarryingDistance = 4f;
    public float CarryingHeight = 2.5f;

public float MouseSensitivity = 2f;
    public float RotationSmoothTime = 0.1f;
    public float MinPitch = -30f;
    public float MaxPitch = 60f;
    public LayerMask CollisionLayers = ~0;
    public float SmoothTransitionSpeed = 5f;

    [Header("Player Reference")]
    public RoachController PlayerController;

    private float _yaw;
    private float _pitch;
    private Vector3 _currentRotation;
    private Vector3 _rotationSmoothVelocity;

    private float _currentDistance;
    private float _currentHeight;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Vector3 angles = transform.eulerAngles;
        _yaw = angles.y;
        _pitch = angles.x;

        _currentDistance = DefaultDistance;
        _currentHeight = DefaultHeight;
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

        float targetDistance = DefaultDistance;
        float targetHeight = DefaultHeight;

        if (PlayerController != null)
        {
            switch (PlayerController._currentState)
            {
                case RoachController.PlayerState.WallClimbing:
                    targetDistance = WallClimbDistance;
                    targetHeight = WallClimbHeight;
                    break;

                case RoachController.PlayerState.Carrying:
                    targetDistance = CarryingDistance;
                    targetHeight = CarryingHeight;

                    // Ajustement plus subtil selon la taille de l'objet
                    Collider carriedCollider = PlayerController.GetCarriedCollider();
                    if (carriedCollider != null)
                    {
                        float objectHeight = carriedCollider.bounds.size.y;
                        float objectSizeFactor = Mathf.Clamp(objectHeight, 0.2f, 2f);

                        // Ajuste légèrement la hauteur et la distance
                        targetHeight += objectSizeFactor;
                        targetDistance += objectSizeFactor;
                    }
                    break;

                default:
                    targetDistance = DefaultDistance;
                    targetHeight = DefaultHeight;
                    break;
            }
        }

        // Transition fluide
        _currentDistance = Mathf.Lerp(_currentDistance, targetDistance, Time.deltaTime * SmoothTransitionSpeed);
        _currentHeight = Mathf.Lerp(_currentHeight, targetHeight, Time.deltaTime * SmoothTransitionSpeed);

        // Rotation de la caméra
        Vector3 targetRotation = new Vector3(_pitch, _yaw);
        _currentRotation = Vector3.SmoothDamp(_currentRotation, targetRotation, ref _rotationSmoothVelocity, RotationSmoothTime);
        Quaternion rot = Quaternion.Euler(_currentRotation.x, _currentRotation.y, 0f);

        // Position avec collision
        Vector3 desiredPosition = Target.position - rot * Vector3.forward * _currentDistance + Vector3.up * _currentHeight;
        if (Physics.Linecast(Target.position + Vector3.up * _currentHeight, desiredPosition, out RaycastHit hit, CollisionLayers))
        {
            desiredPosition = hit.point + (Target.position - hit.point).normalized * 0.3f;
        }

        transform.position = desiredPosition;
        transform.LookAt(Target.position + Vector3.up * _currentHeight);
    }
}
