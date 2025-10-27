using System.Collections;
using PurrNet;
using PurrNet.Logging;
using PurrNet.Transports;
using PurrNet.Steam;
using UnityEngine;

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
        private bool _isHost = false;

        private void Awake()
        {
            if (!TryGetComponent(out _networkManager))
            {
                PurrLogger.LogError($"Failed to get {nameof(NetworkManager)} component.", this);
            }

            _lobbyDataHolder = FindFirstObjectByType<LobbyDataHolder>();
            _isHost = _lobbyDataHolder != null && _lobbyDataHolder.CurrentLobby.IsOwner;
        }

        private void Start()
        {
            if (_steamTransport == null)
            {
                PurrLogger.LogError("SteamTransport is not assigned!", this);
                return;
            }

            // Configurer SteamTransport avec le LobbyId
            if (_lobbyDataHolder != null)
            {
                _steamTransport.name = _lobbyDataHolder.CurrentLobby.LobbyId;
            }

            if (_isHost)
            {
                Debug.Log("Host detected: starting Steam server...");
                _networkManager.transport = _steamTransport;

                // Démarrer le serveur Steam
                _networkManager.StartServer();

                // Démarrer un client local pour le host
                StartCoroutine(StartLocalClient());
            }
            else
            {
                Debug.Log("Client detected: joining Steam server...");
                _networkManager.transport = _steamTransport;

                // Lancer le client
                StartCoroutine(StartClientWithDelay());
            }
        }

        private IEnumerator StartLocalClient()
        {
            yield return new WaitForSeconds(1f); // Attendre que le serveur soit prêt
            Debug.Log("Starting local client on host...");
            _networkManager.StartClient();
        }

        private IEnumerator StartClientWithDelay()
        {
            yield return new WaitForSeconds(2f); // Laisser le temps au host de démarrer
            Debug.Log("Starting client...");
            _networkManager.StartClient();
        }
    }
}
