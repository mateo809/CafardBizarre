using UnityEngine;
using PurrNet;
using UnityEngine.InputSystem;

public class PlayerVehicleController : NetworkBehaviour
{
    [Header("Paramètres")]
    public float enterDistance = 3f;
    public LayerMask vehicleLayer;

    [Header("Références")]
    public Camera playerCamera;
    public float cameraRotationSpeed = 3f;

    [Header("Input System")]
    public InputActionReference moveAction;
    public InputActionReference enterAction;
    public InputActionReference exitAction;
    public InputActionReference brakeAction;
    public InputActionReference lookAction;

    public SyncVar<int> PlayerId = new SyncVar<int>(-1);

    private VehicleController _currentVehicle;
    private int _currentSeat = -1;
    private bool _isDriver => _currentSeat == 0;

    private float _camYaw;
    private float _camPitch;

    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _brakeHeld;

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        if (asServer || !isOwner) return;

        if (playerCamera == null)
            playerCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();

        if (playerCamera != null)
            playerCamera.gameObject.SetActive(true);
    }

    private void OnEnable()
    {
        if (!isOwner) return;

        moveAction?.action.Enable();
        enterAction?.action.Enable();
        exitAction?.action.Enable();
        brakeAction?.action.Enable();
        lookAction?.action.Enable();
    }

    private void OnDisable()
    {
        moveAction?.action.Disable();
        enterAction?.action.Disable();
        exitAction?.action.Disable();
        brakeAction?.action.Disable();
        lookAction?.action.Disable();
    }

    private void Update()
    {
        if (!isOwner) return;

        ReadInputs();

        if (_currentVehicle == null)
        {
            if (enterAction != null && enterAction.action.WasPressedThisFrame())
                HandleEnterInput();

            return;
        }

        if (exitAction != null && exitAction.action.WasPressedThisFrame())
            HandleExitInput();

        if (_isDriver)
            HandleDriverInput();
        else
            HandlePassengerCamera();
    }

    private void ReadInputs()
    {
        _moveInput = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        _lookInput = lookAction != null ? lookAction.action.ReadValue<Vector2>() : Vector2.zero;
        _brakeHeld = brakeAction != null && brakeAction.action.IsPressed();
    }

    private void HandleEnterInput()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, enterDistance, vehicleLayer);
        if (hits == null || hits.Length == 0) return;

        VehicleController closestVehicle = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var vehicle = hit.GetComponentInParent<VehicleController>();
            if (vehicle == null) continue;

            float d = Vector3.Distance(transform.position, vehicle.transform.position);
            if (d < closestDist)
            {
                closestDist = d;
                closestVehicle = vehicle;
            }
        }

        if (closestVehicle == null) return;

        int freeSeat = closestVehicle.GetFreeSeat();
        if (freeSeat == -1)
        {
            Debug.Log("Véhicule plein !");
            return;
        }

        closestVehicle.Server_TrySit(PlayerId.value, freeSeat);
    }

    private void HandleExitInput()
    {
        if (_currentVehicle != null)
            _currentVehicle.Server_TryExit(PlayerId.value);
    }

    private void HandleDriverInput()
    {
        if (_currentVehicle == null) return;

        float motor = _moveInput.y;
        float steer = _moveInput.x;
        bool brake = _brakeHeld;

        _currentVehicle.Server_SendDriverInput(motor, steer, brake);
    }

    private void HandlePassengerCamera()
    {
        if (playerCamera == null) return;

        _camYaw += _lookInput.x * cameraRotationSpeed;
        _camPitch -= _lookInput.y * cameraRotationSpeed;
        _camPitch = Mathf.Clamp(_camPitch, -60f, 60f);

        playerCamera.transform.localRotation = Quaternion.Euler(_camPitch, _camYaw, 0f);
    }

    public void OnEnteredVehicle(VehicleController vehicle, int seatIndex)
    {
        _currentVehicle = vehicle;
        _currentSeat = seatIndex;

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (!_isDriver)
        {
            _camYaw = 0f;
            _camPitch = 0f;
        }

        Debug.Log($"[{PlayerId.value}] Assis siège {seatIndex} ({(_isDriver ? "Conducteur" : "Passager")})");
    }

    public void OnExitedVehicle(Vector3 exitPosition)
    {
        _currentVehicle = null;
        _currentSeat = -1;

        transform.position = exitPosition;

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = true;

        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        if (playerCamera != null)
            playerCamera.transform.localRotation = Quaternion.identity;

        Debug.Log($"[{PlayerId.value}] Sorti du véhicule");
    }
}