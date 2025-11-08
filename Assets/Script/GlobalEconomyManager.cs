using PurrNet;
using TMPro;
using UnityEngine;

public class GlobalEconomyManager : NetworkBehaviour
{
    [Header("Timer Settings")]
    public SyncVar<float> TimeDuration = new SyncVar<float>(300);
    public SyncVar<int> totalMoney = new SyncVar<int>(0);

    // Synchroniser le temps serveur avec les clients
    public SyncVar<float> serverStartTime = new SyncVar<float>(-1);
    public SyncVar<bool> timerStarted = new SyncVar<bool>(false);

    [Header("Player Spawn Settings")]
    [SerializeField] private int expectedPlayerCount = 2;
    private int _currentPlayerCount = 0;

    private static GlobalEconomyManager _instance;

    protected override void OnSpawned()
    {
        base.OnSpawned();
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void Update()
    {
        if (!isServer) return;

        if (!timerStarted.value && IntroManager.IntroFinished)
        {
            CountPlayers();

            if (_currentPlayerCount >= expectedPlayerCount)
            {
                StartTimer();
            }
        }
    }

    private void CountPlayers()
    {
        _currentPlayerCount = FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None).Length;

        int playerCount = 0;
        foreach (var identity in FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None))
        {
            // Compte seulement les objets qui ont un PlayerHealth (= c'est un joueur)
            if (identity.GetComponent<PlayerHealth>() != null)
            {
                playerCount++;
            }
        }

        _currentPlayerCount = playerCount;
    }

    private void StartTimer()
    {
        serverStartTime.value = Time.time;
        timerStarted.value = true;
        Debug.Log($"[GlobalEconomyManager] Timer démarré! {_currentPlayerCount} joueurs détectés.");
    }

    public float GetElapsedTime()
    {
        if (!timerStarted.value || serverStartTime.value < 0) return 0f;

        // TOUS les clients utilisent le MÊME serverStartTime reçu du serveur
        return Time.realtimeSinceStartup - serverStartTime.value;
    }

    public float GetRemainingTime()
    {
        if (!timerStarted.value) return TimeDuration.value;
        return Mathf.Max(0, TimeDuration.value - GetElapsedTime());
    }

    public void AddMoneyServerRpc(int amount)
    {
        if (isServer)
        {
            totalMoney.value += amount;
        }
    }

    public int GetTotalMoney()
    {
        return totalMoney.value;
    }

    public bool IsTimerRunning()
    {
        return timerStarted.value;
    }
}