//using UnityEngine;
//using PurrNet;

//public class VehicleController : NetworkBehaviour
//{
//    [Header("Sièges")]
//    public Transform[] seatPoints = new Transform[4];

//    [Header("Physique")]
//    public float motorForce = 1500f;
//    public float brakeForce = 3000f;
//    public float maxSteerAngle = 30f;

//    [Header("Roues")]
//    public WheelCollider frontLeftWheel;
//    public WheelCollider frontRightWheel;
//    public WheelCollider rearLeftWheel;
//    public WheelCollider rearRightWheel;

//    public Transform frontLeftMesh;
//    public Transform frontRightMesh;
//    public Transform rearLeftMesh;
//    public Transform rearRightMesh;

//    // Stocke les références directes aux joueurs assis (null = libre)
//    private readonly SyncVar<NetworkIdentity> seat0 = new SyncVar<NetworkIdentity>(null);
//    private readonly SyncVar<NetworkIdentity> seat1 = new SyncVar<NetworkIdentity>(null);
//    private readonly SyncVar<NetworkIdentity> seat2 = new SyncVar<NetworkIdentity>(null);
//    private readonly SyncVar<NetworkIdentity> seat3 = new SyncVar<NetworkIdentity>(null);

//    private readonly SyncVar<float> syncedMotor = new SyncVar<float>(0f);
//    private readonly SyncVar<float> syncedSteer = new SyncVar<float>(0f);
//    private readonly SyncVar<bool> syncedBrake = new SyncVar<bool>(false);

//    private Rigidbody _rb;

//    private void Awake() => _rb = GetComponent<Rigidbody>();

//    private void FixedUpdate()
//    {
//        if (!isServer) return;
//        ApplyMotor(syncedMotor.value, syncedSteer.value, syncedBrake.value);
//        UpdateWheelMeshes();
//    }

//    // ??? API publique ?????????????????????????????????????????????????????????

//    public int GetFreeSeat()
//    {
//        if (seat0.value == null) return 0;
//        if (seat1.value == null) return 1;
//        if (seat2.value == null) return 2;
//        if (seat3.value == null) return 3;
//        return -1;
//    }

//    public Transform GetSeatTransform(int seatIndex)
//    {
//        if (seatIndex < 0 || seatIndex >= seatPoints.Length) return null;
//        return seatPoints[seatIndex];
//    }


//    [ServerRpc(requireOwnership: false)]
//    public void Server_TrySit(RoachController1 player, int seatIndex)
//    {
//        if (player == null) return;
//        if (seatIndex < 0 || seatIndex > 3) return;

//        var netId = player.GetComponent<NetworkIdentity>();
//        if (netId == null) return;

//        // Siège déjà occupé ?
//        if (GetSeatOccupant(seatIndex) != null) return;

//        // Joueur déjà dans ce véhicule ?
//        for (int i = 0; i < 4; i++)
//            if (GetSeatOccupant(i) == netId) return;

//        SetSeatOccupant(seatIndex, netId);

//        // Donner ownership au conducteur
//        if (seatIndex == 0)
//            GiveOwnership(player.owner, false);

//        syncedBrake.value = false;

//        Observers_OnEntered(player.gameObject, seatIndex);
//    }

//    [ServerRpc(requireOwnership: false)]
//    public void Server_TryExit(RoachController1 player)
//    {
//        if (player == null) return;

//        var netId = player.GetComponent<NetworkIdentity>();
//        if (netId == null) return;

//        for (int i = 0; i < 4; i++)
//        {
//            if (GetSeatOccupant(i) != netId) continue;

//            SetSeatOccupant(i, null);

//            if (i == 0)
//            {
//                syncedMotor.value = 0f;
//                syncedSteer.value = 0f;
//                syncedBrake.value = true;
//                RemoveOwnership();
//            }

//            Observers_OnExited(player.gameObject, GetExitPosition(i));
//            return;
//        }
//    }

//    [ServerRpc(requireOwnership: true)]
//    public void Server_SendDriverInput(float motor, float steer, bool brake)
//    {
//        if (seat0.value == null) return;

//        syncedMotor.value = Mathf.Clamp(motor, -1f, 1f);
//        syncedSteer.value = Mathf.Clamp(steer, -1f, 1f);
//        syncedBrake.value = brake;
//    }


//    [ObserversRpc]
//    private void Observers_OnEntered(GameObject playerGo, int seatIndex)
//    {
//        if (playerGo == null) return;
//        var player = playerGo.GetComponent<RoachController1>();
//        if (player != null)
//            player.OnEnteredVehicle(this, seatIndex);
//    }

//    [ObserversRpc]
//    private void Observers_OnExited(GameObject playerGo, Vector3 exitPos)
//    {
//        if (playerGo == null) return;
//        var player = playerGo.GetComponent<RoachController1>();
//        if (player != null)
//            player.OnExitedVehicle(exitPos);
//    }


//    private void ApplyMotor(float motor, float steer, bool brake)
//    {
//        float actualBrake = brake ? brakeForce : 0f;
//        float actualMotor = brake ? 0f : motor * motorForce;

//        frontLeftWheel.motorTorque = actualMotor;
//        frontRightWheel.motorTorque = actualMotor;

//        frontLeftWheel.brakeTorque = actualBrake;
//        frontRightWheel.brakeTorque = actualBrake;
//        rearLeftWheel.brakeTorque = actualBrake;
//        rearRightWheel.brakeTorque = actualBrake;

//        float steerAngle = steer * maxSteerAngle;
//        frontLeftWheel.steerAngle = steerAngle;
//        frontRightWheel.steerAngle = steerAngle;
//    }

//    private void UpdateWheelMeshes()
//    {
//        UpdateSingleWheel(frontLeftWheel, frontLeftMesh);
//        UpdateSingleWheel(frontRightWheel, frontRightMesh);
//        UpdateSingleWheel(rearLeftWheel, rearLeftMesh);
//        UpdateSingleWheel(rearRightWheel, rearRightMesh);
//    }

//    private void UpdateSingleWheel(WheelCollider col, Transform mesh)
//    {
//        if (mesh == null || col == null) return;
//        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
//        mesh.SetPositionAndRotation(pos, rot);
//    }


//    private NetworkIdentity GetSeatOccupant(int i) => i switch
//    {
//        0 => seat0.value,
//        1 => seat1.value,
//        2 => seat2.value,
//        3 => seat3.value,
//        _ => null
//    };

//    private void SetSeatOccupant(int i, NetworkIdentity id)
//    {
//        switch (i)
//        {
//            case 0: seat0.value = id; break;
//            case 1: seat1.value = id; break;
//            case 2: seat2.value = id; break;
//            case 3: seat3.value = id; break;
//        }
//    }

//    private Vector3 GetExitPosition(int seatIndex)
//    {
//        if (seatIndex < 0 || seatIndex >= seatPoints.Length || seatPoints[seatIndex] == null)
//            return transform.position + transform.right * 2f;

//        return seatPoints[seatIndex].position + transform.right * 2f;
//    }
//}