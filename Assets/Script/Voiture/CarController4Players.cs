using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class CarController4Players : MonoBehaviour
{
    [Header("Car")]
    public Rigidbody rb;
    public float acceleration = 25f;
    public float steering = 55f;
    public float maxSpeed = 20f;

    [Header("Seats")]
    public Transform driverSeat;
    public Transform[] passengerSeats = new Transform[3];

    [Header("Input")]
    public InputActionReference interactAction;

    private GameObject driverPlayer;
    private readonly List<GameObject> passengers = new List<GameObject>();
    private GameObject nearbyPlayer;

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        interactAction?.action.Enable();
    }

    void OnDisable()
    {
        interactAction?.action.Disable();
    }

    void Update()
    {
        if (driverPlayer != null)
        {
            float v = Input.GetAxis("Vertical");
            float h = Input.GetAxis("Horizontal");
            MoveCar(v, h);
        }

        if (interactAction != null && interactAction.action != null && interactAction.action.WasPressedThisFrame())
        {
            OnInteractPressed();
        }
    }

    void MoveCar(float vertical, float horizontal)
    {
        if (rb.linearVelocity.magnitude < maxSpeed)
            rb.AddForce(transform.forward * vertical * acceleration, ForceMode.Acceleration);

        float steer = horizontal * steering * Time.deltaTime;
        transform.Rotate(0f, steer, 0f);
    }

    private void OnInteractPressed()
    {
        if (nearbyPlayer == null) return;

        if (driverPlayer == nearbyPlayer || passengers.Contains(nearbyPlayer))
            ExitCar(nearbyPlayer);
        else
            TryEnterCar(nearbyPlayer);
    }

    public void SetNearbyPlayer(GameObject player)
    {
        nearbyPlayer = player;
    }

    public void ClearNearbyPlayer(GameObject player)
    {
        if (nearbyPlayer == player)
            nearbyPlayer = null;
    }

    public bool TryEnterCar(GameObject player)
    {
        if (driverPlayer == null)
        {
            driverPlayer = player;
            SeatPlayer(player, driverSeat);
            return true;
        }

        if (passengers.Count < passengerSeats.Length)
        {
            passengers.Add(player);
            SeatPlayer(player, passengerSeats[passengers.Count - 1]);
            return true;
        }

        return false;
    }

    public void ExitCar(GameObject player)
    {
        if (driverPlayer == player)
            driverPlayer = null;

        if (passengers.Contains(player))
            passengers.Remove(player);

        player.transform.SetParent(null);
    }

    void SeatPlayer(GameObject player, Transform seat)
    {
        player.transform.SetParent(seat);
        player.transform.localPosition = Vector3.zero;
        player.transform.localRotation = Quaternion.identity;
    }

    public bool IsDriver(GameObject player)
    {
        return player == driverPlayer;
    }
}