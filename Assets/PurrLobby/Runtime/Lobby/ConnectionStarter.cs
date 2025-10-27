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

            if (_lobbyDataHolder == null)
            {
                PurrLogger.LogError("LobbyDataHolder not found!", this);
                return;
            }

            Debug.Log($"SteamTransport address set to lobby ID: {_steamTransport.address}");

            _networkManager.transport = _steamTransport;

            if (_isHost)
            {
                Debug.Log("Host detected: starting Steam server...");
                _networkManager.StartServer();

                // Démarrer un client local sur le host après un délai
                StartCoroutine(StartLocalClient());
            }
            else
            {
                Debug.Log("Client detected: joining Steam server...");
                StartCoroutine(StartClientWithDelay());
            }
        }

        private IEnumerator StartLocalClient()
        {
            // Attendre que le serveur Steam soit pleinement prêt
            float waitTime = 3f;
            Debug.Log($"Waiting {waitTime} seconds before starting local client...");
            yield return new WaitForSeconds(waitTime);

            Debug.Log("Starting local client on host...");
            _networkManager.StartClient();
        }

        private IEnumerator StartClientWithDelay()
        {
            // Délai pour laisser le host démarrer
            float waitTime = 3f;
            Debug.Log($"Waiting {waitTime} seconds before starting client...");
            yield return new WaitForSeconds(waitTime);

            Debug.Log($"Client attempting to connect to lobby ID: {_steamTransport.address}");
            _networkManager.StartClient();
        }
    }
}
