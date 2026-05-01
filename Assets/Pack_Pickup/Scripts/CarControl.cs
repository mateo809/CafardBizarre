using System.Collections.Generic;
using UnityEngine;
using PurrNet;

public class CarControl : NetworkBehaviour
{
    [Header("Physique")]
    public float enginePower = 2000f;
    public float turnSpeed = 25f;
    public float turnSmoothness = 5f;
    public Transform centerOfMass;

    [Header("Roues")]
    public Transform[] wheels;
    public Transform[] wheelMeshes;

    [Header("Volant")]
    public GameObject steeringWheel;

    [Header("Sièges")]
    public Transform[] seatPoints = new Transform[4];

    [Header("Audio")]
    [SerializeField] private AudioType carAudioType = AudioType.Car;
    [SerializeField] private AudioSourceType carAudioSourceType = AudioSourceType.Game;

    private readonly SyncVar<string> seat0 = new SyncVar<string>("");
    private readonly SyncVar<string> seat1 = new SyncVar<string>("");
    private readonly SyncVar<string> seat2 = new SyncVar<string>("");
    private readonly SyncVar<string> seat3 = new SyncVar<string>("");

    private readonly SyncVar<float> syncedMotor = new SyncVar<float>(0f);
    private readonly SyncVar<float> syncedSteer = new SyncVar<float>(0f);
    private readonly SyncVar<bool> syncedBrake = new SyncVar<bool>(false);

    private Rigidbody _rb;
    private float _currentTurnAngle = 0f;
    private bool _radioPlayed;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb != null)
        {
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.maxAngularVelocity = 20f;
        }

        if (centerOfMass != null && _rb != null)
            _rb.centerOfMass = centerOfMass.localPosition;
    }

    private void FixedUpdate()
    {
        if (!isServer) return;
        ApplyDrive(syncedMotor.value, syncedSteer.value, syncedBrake.value);
    }

    private void LateUpdate()
    {
        UpdateWheelMeshes();
        UpdateSteeringWheelVisual();
    }

    public int GetFreeSeat()
    {
        if (string.IsNullOrEmpty(seat0.value)) return 0;
        if (string.IsNullOrEmpty(seat1.value)) return 1;
        if (string.IsNullOrEmpty(seat2.value)) return 2;
        if (string.IsNullOrEmpty(seat3.value)) return 3;
        return -1;
    }

    public Transform GetSeatTransform(int seatIndex)
    {
        if (seatIndex < 0 || seatIndex >= seatPoints.Length) return null;
        return seatPoints[seatIndex];
    }

    [ServerRpc(requireOwnership: false)]
    public void Server_TrySit(RoachController1 player)
    {
        if (player == null) return;

        string id = player.steamId.value;
        if (string.IsNullOrEmpty(id) || id == "0") return;

        for (int i = 0; i < 4; i++)
            if (GetSeatOccupant(i) == id)
                return;

        int freeSeat = GetFreeSeat();
        if (freeSeat == -1) return;

        SetSeatOccupant(freeSeat, id);

        if (freeSeat == 0)
        {
            GiveOwnership(player.owner, false);
            PlayCarSound();
        }

        syncedBrake.value = false;
        Observers_OnEntered(player.gameObject, freeSeat);
    }

    [ServerRpc(requireOwnership: false)]
    public void Server_TryExit(RoachController1 player)
    {
        if (player == null) return;

        string id = player.steamId.value;
        if (string.IsNullOrEmpty(id)) return;

        int seatIndex = -1;
        for (int i = 0; i < 4; i++)
        {
            if (GetSeatOccupant(i) == id)
            {
                seatIndex = i;
                break;
            }
        }

        if (seatIndex == -1) return;

        SetSeatOccupant(seatIndex, "");

        if (seatIndex == 0)
        {
            syncedMotor.value = 0f;
            syncedSteer.value = 0f;
            syncedBrake.value = true;
            RemoveOwnership();
        }

        Observers_OnExited(player.gameObject, GetExitPosition(seatIndex));
    }

    [ServerRpc(requireOwnership: true)]
    public void Server_SendDriverInput(float motor, float steer, bool brake)
    {
        if (string.IsNullOrEmpty(seat0.value)) return;

        syncedMotor.value = Mathf.Clamp(motor, -1f, 1f);
        syncedSteer.value = Mathf.Clamp(steer, -1f, 1f);
        syncedBrake.value = brake;
    }

    [ObserversRpc]
    private void Observers_OnEntered(GameObject playerGo, int seatIndex)
    {
        if (playerGo == null) return;

        var player = playerGo.GetComponent<RoachController1>();
        if (player != null)
            player.OnEnteredVehicle(this, seatIndex);
    }

    [ObserversRpc]
    private void Observers_OnExited(GameObject playerGo, Vector3 exitPos)
    {
        if (playerGo == null) return;

        var player = playerGo.GetComponent<RoachController1>();
        if (player != null)
            player.OnExitedVehicle(exitPos);
        StopCarSound();
    }

    private void ApplyDrive(float motor, float steer, bool brake)
    {
        float targetTurnAngle = steer * turnSpeed;
        _currentTurnAngle = Mathf.Lerp(_currentTurnAngle, targetTurnAngle, Time.fixedDeltaTime * turnSmoothness);

        float motorTorque = brake ? 0f : motor * enginePower;
        float brakeTorque = brake ? enginePower * 2f : 0f;

        for (int i = 0; i < wheels.Length; i++)
        {
            WheelCollider wc = wheels[i].GetComponent<WheelCollider>();
            if (wc == null) continue;

            wc.steerAngle = (i < 2) ? _currentTurnAngle : 0f;
            wc.motorTorque = motorTorque;
            wc.brakeTorque = brakeTorque;
        }
    }

    private void UpdateWheelMeshes()
    {
        for (int i = 0; i < wheels.Length && i < wheelMeshes.Length; i++)
        {
            WheelCollider wc = wheels[i].GetComponent<WheelCollider>();
            if (wc == null || wheelMeshes[i] == null) continue;

            wc.GetWorldPose(out Vector3 pos, out Quaternion rot);
            wheelMeshes[i].SetPositionAndRotation(pos, rot);
        }
    }

    private void UpdateSteeringWheelVisual()
    {
        if (steeringWheel != null)
            steeringWheel.transform.localEulerAngles = new Vector3(-64f, 0f, _currentTurnAngle * 3f);
    }

    private string GetSeatOccupant(int i) => i switch
    {
        0 => seat0.value,
        1 => seat1.value,
        2 => seat2.value,
        3 => seat3.value,
        _ => ""
    };

    private void SetSeatOccupant(int i, string id)
    {
        switch (i)
        {
            case 0: seat0.value = id; break;
            case 1: seat1.value = id; break;
            case 2: seat2.value = id; break;
            case 3: seat3.value = id; break;
        }
    }

    private Vector3 GetExitPosition(int seatIndex)
    {
        if (seatIndex < 0 || seatIndex >= seatPoints.Length || seatPoints[seatIndex] == null)
            return transform.position + transform.right * 2f;

        return seatPoints[seatIndex].position + transform.right * 2f;
    }

    // CORRECTION : PlayLoopedSound au lieu de PlaySound
    private void PlayCarSound()
    {
        if (_radioPlayed) return;
        _radioPlayed = true;

        AudioController.Instance.PlayLoopedSound(carAudioType, carAudioSourceType);
    }

    // CORRECTION : carAudioType au lieu de AudioType.Car en dur
    private void StopCarSound()
    {
        if (!_radioPlayed) return;
        _radioPlayed = false;

        AudioController.Instance.StopSound(carAudioType);
    }
}