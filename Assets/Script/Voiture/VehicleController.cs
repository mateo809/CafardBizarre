using UnityEngine;
using PurrNet;

public class VehicleController : NetworkBehaviour
{
    [Header("Sièges")]
    public Transform[] seatPoints = new Transform[4];

    [Header("Physique")]
    public float motorForce = 1500f;
    public float brakeForce = 3000f;
    public float maxSteerAngle = 30f;

    [Header("Roues")]
    public WheelCollider frontLeftWheel;
    public WheelCollider frontRightWheel;
    public WheelCollider rearLeftWheel;
    public WheelCollider rearRightWheel;

    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    private readonly SyncVar<int> seat0OccupantId = new SyncVar<int>(-1);
    private readonly SyncVar<int> seat1OccupantId = new SyncVar<int>(-1);
    private readonly SyncVar<int> seat2OccupantId = new SyncVar<int>(-1);
    private readonly SyncVar<int> seat3OccupantId = new SyncVar<int>(-1);

    private readonly SyncVar<float> syncedMotor = new SyncVar<float>(0f);
    private readonly SyncVar<float> syncedSteer = new SyncVar<float>(0f);
    private readonly SyncVar<bool> syncedBrake = new SyncVar<bool>(false);

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (!isServer) return;

        ApplyMotor(syncedMotor.value, syncedSteer.value, syncedBrake.value);
        UpdateWheelMeshes();
    }

    public int GetFreeSeat()
    {
        if (seat0OccupantId.value == -1) return 0;
        if (seat1OccupantId.value == -1) return 1;
        if (seat2OccupantId.value == -1) return 2;
        if (seat3OccupantId.value == -1) return 3;
        return -1;
    }

    public int GetOccupant(int seatIndex)
    {
        return seatIndex switch
        {
            0 => seat0OccupantId.value,
            1 => seat1OccupantId.value,
            2 => seat2OccupantId.value,
            3 => seat3OccupantId.value,
            _ => -1
        };
    }

    private void SetOccupant(int seatIndex, int playerId)
    {
        switch (seatIndex)
        {
            case 0: seat0OccupantId.value = playerId; break;
            case 1: seat1OccupantId.value = playerId; break;
            case 2: seat2OccupantId.value = playerId; break;
            case 3: seat3OccupantId.value = playerId; break;
        }
    }

    [ServerRpc(requireOwnership: false)]
    public void Server_TrySit(int playerId, int seatIndex)
    {
        if (seatIndex < 0 || seatIndex > 3) return;
        if (GetOccupant(seatIndex) != -1) return;

        for (int i = 0; i < 4; i++)
            if (GetOccupant(i) == playerId)
                return;

        SetOccupant(seatIndex, playerId);

        var player = FindPlayerById(playerId);
        if (player != null)
            player.OnEnteredVehicle(this, seatIndex);

        if (seatIndex == 0)
        {
            var pid = GetPlayerIdFromPlayer(player);
            if (pid != null)
                GiveOwnership(pid, false);
        }

        syncedBrake.value = false;
    }

    [ServerRpc(requireOwnership: false)]
    public void Server_TryExit(int playerId)
    {
        for (int i = 0; i < 4; i++)
        {
            if (GetOccupant(i) != playerId) continue;

            SetOccupant(i, -1);

            if (i == 0)
            {
                syncedMotor.value = 0f;
                syncedSteer.value = 0f;
                syncedBrake.value = true;
                RemoveOwnership();
            }

            var player = FindPlayerById(playerId);
            if (player != null)
                player.OnExitedVehicle(GetExitPosition(i));

            return;
        }
    }

    [ServerRpc(requireOwnership: true)]
    public void Server_SendDriverInput(float motor, float steer, bool brake)
    {
        if (seat0OccupantId.value == -1) return;

        syncedMotor.value = Mathf.Clamp(motor, -1f, 1f);
        syncedSteer.value = Mathf.Clamp(steer, -1f, 1f);
        syncedBrake.value = brake;
    }

    private void ApplyMotor(float motor, float steer, bool brake)
    {
        float actualBrake = brake ? brakeForce : 0f;
        float actualMotor = brake ? 0f : motor * motorForce;

        frontLeftWheel.motorTorque = actualMotor;
        frontRightWheel.motorTorque = actualMotor;

        frontLeftWheel.brakeTorque = actualBrake;
        frontRightWheel.brakeTorque = actualBrake;
        rearLeftWheel.brakeTorque = actualBrake;
        rearRightWheel.brakeTorque = actualBrake;

        float steerAngle = steer * maxSteerAngle;
        frontLeftWheel.steerAngle = steerAngle;
        frontRightWheel.steerAngle = steerAngle;
    }

    private void UpdateWheelMeshes()
    {
        UpdateSingleWheel(frontLeftWheel, frontLeftMesh);
        UpdateSingleWheel(frontRightWheel, frontRightMesh);
        UpdateSingleWheel(rearLeftWheel, rearLeftMesh);
        UpdateSingleWheel(rearRightWheel, rearRightMesh);
    }

    private void UpdateSingleWheel(WheelCollider col, Transform mesh)
    {
        if (mesh == null || col == null) return;
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        mesh.SetPositionAndRotation(pos, rot);
    }

    private PlayerVehicleController FindPlayerById(int id)
    {
        foreach (var p in FindObjectsByType<PlayerVehicleController>(FindObjectsSortMode.None))
        {
            if (p.PlayerId.value == id)
                return p;
        }
        return null;
    }

    private PlayerID GetPlayerIdFromPlayer(PlayerVehicleController player)
    {
        if (player == null) return default;
        return (PlayerID)(object)player.PlayerId.value;
    }

    private Vector3 GetExitPosition(int seatIndex)
    {
        if (seatIndex < 0 || seatIndex >= seatPoints.Length || seatPoints[seatIndex] == null)
            return transform.position + transform.right * 2f;

        return seatPoints[seatIndex].position + transform.right * 2f;
    }

    public Transform GetSeatTransform(int seatIndex) => seatPoints[seatIndex];
}