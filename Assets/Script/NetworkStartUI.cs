using System;
using System.Threading.Tasks;
using UnityEngine;
using Steamworks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class NetworkStartUI : MonoBehaviour
{
    public GameObject playerPrefab;
    public int maxPlayers = 4;

    private CSteamID _currentLobby = CSteamID.Nil;
    private bool _isHost = false;
    private bool _isQuitting = false;

    private CallResult<LobbyCreated_t> _lobbyCreated;
    private CallResult<LobbyEnter_t> _lobbyEnter;

    public ulong hostSteamID;

    private void Start()
    {
        InitializeSteam();
    }

    private void InitializeSteam()
    {
        if (!SteamAPI.Init())
        {
            Debug.LogError("SteamAPI initialization failed.");
            return;
        }

        Debug.Log("Steam initialized!");
        _ = SetupLobbyAsync();
    }

    private async Task SetupLobbyAsync()
    {
        if (SteamUser.GetSteamID().m_SteamID == hostSteamID)
        {
            Debug.Log("I am host, creating lobby...");
            await CreateLobbyAsync();
        }
        else
        {
            // Sinon rejoindre le lobby du host
            Debug.Log("I am client, joining lobby...");
            await JoinLobbyAsync(hostSteamID);
        }
    }

    private async Task CreateLobbyAsync()
    {
        var handle = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxPlayers);
        var tcs = new TaskCompletionSource<CSteamID>();

        _lobbyCreated = CallResult<LobbyCreated_t>.Create();
        _lobbyCreated.Set(handle, (result, ioError) =>
        {
            if (result.m_eResult == EResult.k_EResultOK)
            {
                _currentLobby = new CSteamID(result.m_ulSteamIDLobby);
                _isHost = true;
                Debug.Log($"Lobby created ID: {_currentLobby.m_SteamID}");
                tcs.TrySetResult(_currentLobby);
            }
            else
            {
                Debug.LogError("Failed to create lobby");
                tcs.TrySetResult(CSteamID.Nil);
            }
        });

        await tcs.Task;

        if (_currentLobby != CSteamID.Nil)
            StartNetwork();
    }

    private async Task JoinLobbyAsync(ulong lobbyOwnerSteamID)
    {
        var cLobby = new CSteamID(lobbyOwnerSteamID);
        var handle = SteamMatchmaking.JoinLobby(cLobby);
        var tcs = new TaskCompletionSource<bool>();

        _lobbyEnter = CallResult<LobbyEnter_t>.Create();
        _lobbyEnter.Set(handle, (result, ioError) =>
        {
            if (result.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                _currentLobby = new CSteamID(result.m_ulSteamIDLobby);
                _isHost = SteamMatchmaking.GetLobbyOwner(_currentLobby) == SteamUser.GetSteamID();
                Debug.Log($"Joined lobby: {_currentLobby.m_SteamID}, isHost: {_isHost}");
                tcs.TrySetResult(true);
            }
            else
            {
                Debug.LogError("Failed to join lobby");
                tcs.TrySetResult(false);
            }
        });

        await tcs.Task;

        if (_currentLobby != CSteamID.Nil)
            StartNetwork();
    }

    private void StartNetwork()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager is missing!");
            return;
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("UnityTransport component missing on NetworkManager.");
            return;
        }

        if (_isHost)
        {
            transport.ConnectionData.Port = 7777;
            Debug.Log("Starting as host...");
            NetworkManager.Singleton.StartHost();

            SpawnPlayer(NetworkManager.Singleton.LocalClientId);
        }
        else
        {
            // Remplacer par l'IP du host si distant
            transport.ConnectionData.Address = "127.0.0.1";
            transport.ConnectionData.Port = 7777;
            Debug.Log("Starting as client...");
            NetworkManager.Singleton.StartClient();
        }

        NetworkManager.Singleton.OnClientConnectedCallback += SpawnPlayer;
    }

    private void SpawnPlayer(ulong clientId)
    {
        if (NetworkManager.Singleton == null || playerPrefab == null) return;

        GameObject player = Instantiate(playerPrefab);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
        Debug.Log($"Player spawned for client {clientId}");
    }

    private void Update()
    {
        if (!_isQuitting)
            SteamAPI.RunCallbacks();
    }

    private void OnApplicationQuit()
    {
        _isQuitting = true;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= SpawnPlayer;

            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
                NetworkManager.Singleton.Shutdown();
        }

        if (SteamAPI.IsSteamRunning())
            SteamAPI.Shutdown();
    }
}
