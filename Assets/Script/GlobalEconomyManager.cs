using PurrNet;
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

    public static GlobalEconomyManager _instance;

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
        // Seul le serveur gère la logique de démarrage du timer
        if (!isServer) return;

        if (!timerStarted.value && IntroManager.IntroFinished)
        {
            CountPlayers();

            if (_currentPlayerCount >= expectedPlayerCount && _currentPlayerCount > 0)
            {
                StartTimer();
            }
        }
    }

    private void CountPlayers()
    {
        int playerCount = 0;
        foreach (var identity in FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None))
        {
            if (identity.GetComponent<PlayerHealth>() != null)
            {
                playerCount++;
            }
        }
        _currentPlayerCount = playerCount;
    }

    private void StartTimer()
    {
        // On définit le point de départ en utilisant le temps de la simulation serveur
        serverStartTime.value = Time.time;
        timerStarted.value = true;
        Debug.Log($"[GlobalEconomyManager] Timer démarré! {_currentPlayerCount} joueurs détectés.");
    }

    public float GetElapsedTime()
    {
        if (!timerStarted.value || serverStartTime.value < 0) return 0f;

        // On utilise Time.time qui est synchronisé par le serveur via la SyncVar
        // Tous les clients effectuent ce calcul avec la même référence de départ
        return Time.time - serverStartTime.value;
    }

    public float GetRemainingTime()
    {
        if (!timerStarted.value) return TimeDuration.value;
        return Mathf.Max(0, TimeDuration.value - GetElapsedTime());
    }

    [ServerRpc]
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