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
    public float CarryingBaseDistance = 4f;
    public float CarryingBaseHeight = 2.5f;

    [Header("Adaptation multipliers")]
    public float CarryingDistanceMultiplier = 0.6f; // influence de la taille sur le recul
    public float CarryingHeightMultiplier = 0.3f;   // influence de la taille sur la hauteur

    [Header("Camera Movement")]
    public float MouseSensitivity = 2f;
    public float ZoomSpeed = 2f;
    public float MinDistance = 2f;
    public float MaxDistance = 10f;
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
    private float _targetDistance;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Vector3 angles = transform.eulerAngles;
        _yaw = angles.y;
        _pitch = angles.x;

        _currentDistance = DefaultDistance;
        _targetDistance = DefaultDistance;
        _currentHeight = DefaultHeight;
    }

    public void OnLook(InputAction.CallbackContext ctx)
    {
        Vector2 look = ctx.ReadValue<Vector2>();
        _yaw += look.x * MouseSensitivity;
        _pitch -= look.y * MouseSensitivity;
        _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
    }

    public void OnZoom(InputAction.CallbackContext ctx)
    {
        float scroll = ctx.ReadValue<Vector2>().y;
        _targetDistance -= scroll * ZoomSpeed;
        _targetDistance = Mathf.Clamp(_targetDistance, MinDistance, MaxDistance);
    }

    private void LateUpdate()
    {
        if (!Target) return;

        float targetHeight = DefaultHeight;
        Vector3 targetPosition = Target.position;

        if (PlayerController != null)
        {
            switch (PlayerController._currentState)
            {
                case RoachController.PlayerState.WallClimbing:
                    _targetDistance = Mathf.Lerp(_targetDistance, MaxDistance, Time.deltaTime * SmoothTransitionSpeed);
                    targetHeight = WallClimbHeight;
                    break;

                case RoachController.PlayerState.Carrying:
                    if (PlayerController.carriedObject != null)
                    {
                        Renderer objRenderer = PlayerController.carriedObject.GetComponent<Renderer>();
                        float objectSize = objRenderer ? objRenderer.bounds.size.magnitude : 1f;

                        float adaptedDistance = CarryingBaseDistance + objectSize * CarryingDistanceMultiplier;
                        float adaptedHeight = CarryingBaseHeight + objectSize * CarryingHeightMultiplier;

                        _targetDistance = Mathf.Clamp(adaptedDistance, MinDistance, MaxDistance);
                        targetHeight = adaptedHeight;
                    }
                    else
                    {
                        _targetDistance = DefaultDistance;
                        targetHeight = CarryingBaseHeight;
                    }
                    break;

                default:
                    _targetDistance = DefaultDistance;
                    targetHeight = DefaultHeight;
                    break;
            }
        }

        // Lerp vers les valeurs cibles
        _currentDistance = Mathf.Lerp(_currentDistance, _targetDistance, Time.deltaTime * SmoothTransitionSpeed);
        _currentHeight = Mathf.Lerp(_currentHeight, targetHeight, Time.deltaTime * SmoothTransitionSpeed);

        // Rotation de la caméra
        Vector3 targetRotation = new Vector3(_pitch, _yaw);
        _currentRotation = Vector3.SmoothDamp(_currentRotation, targetRotation, ref _rotationSmoothVelocity, RotationSmoothTime);
        Quaternion rot = Quaternion.Euler(_currentRotation.x, _currentRotation.y, 0f);

        // Position avec gestion des collisions
        Vector3 desiredPosition = targetPosition - rot * Vector3.forward * _currentDistance + Vector3.up * _currentHeight;
        if (Physics.Linecast(targetPosition + Vector3.up * _currentHeight, desiredPosition, out RaycastHit hit, CollisionLayers))
        {
            desiredPosition = hit.point + (targetPosition - hit.point).normalized * 0.3f;
        }

        transform.position = desiredPosition;
        transform.LookAt(targetPosition + Vector3.up * _currentHeight);
    }
}
