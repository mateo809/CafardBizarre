using System.Collections;
using PurrNet;
using PurrNet.Logging;
using PurrNet.Transports;
using PurrNet.Steam;
using UnityEngine;
using Steamworks;

#if UTP_LOBBYRELAY
using PurrNet.UTP;
using Unity.Services.Relay.Models;
#endif

namespace PurrLobby
{
    public class SteamConnectionStarter : MonoBehaviour
    {
        [Header("Steam Transport")]
        [SerializeField] private SteamTransport _steamTransport;

        private NetworkManager _networkManager;
        private LobbyDataHolder _lobbyDataHolder;

        private bool _isFromLobby;

        private void Awake()
        {
            if (!TryGetComponent(out _networkManager))
            {
                PurrLogger.LogError($"Failed to get {nameof(NetworkManager)} component.", this);
            }

            _lobbyDataHolder = FindFirstObjectByType<LobbyDataHolder>();
            if (_lobbyDataHolder)
                _isFromLobby = true;

        }

        private void Start()
        {
            if (_steamTransport == null)
            {
                PurrLogger.LogError("SteamTransport is not assigned!", this);
                return;
            }

            if (_lobbyDataHolder == null)
            {
                PurrLogger.LogError("LobbyDataHolder not found!", this);
                return;
            }

            if (_isFromLobby)
                StartFromLobby();
            else
                StartNormal();

        }

        private void StartNormal()
        {
            _networkManager.transport = _steamTransport;
        }

        private void StartFromLobby()
        {
            _networkManager.transport = _steamTransport;

            if (!ulong.TryParse(_lobbyDataHolder.CurrentLobby.LobbyId, out ulong lobbyID))
            {
                PurrLogger.LogError("Failed to parse Steam Lobby ID from lobby data.", this);
                return;
            }

            var lobbyOwner = SteamMatchmaking.GetLobbyOwner(new CSteamID(lobbyID));

            _steamTransport.address = lobbyOwner.ToString();

            if (_lobbyDataHolder.CurrentLobby.IsOwner)
            {
                // Démarre le serveur
                _networkManager.StartServer();

                // Démarre aussi le client local après 1-2 secondes
                StartCoroutine(StartLocalClient());
            }
            else
            {
                // Client externe
                StartCoroutine(StartClient());
            }
        }

        private IEnumerator StartLocalClient()
        {
            yield return new WaitForSeconds(1f); // attendre que le serveur soit prêt
            Debug.Log("Starting local client on host...");
            _networkManager.StartClient();
        }

        private IEnumerator StartClient()
        {
            yield return new WaitForSeconds(3f); // attendre que le host soit prêt
            _networkManager.StartClient();
        }

    }
}


