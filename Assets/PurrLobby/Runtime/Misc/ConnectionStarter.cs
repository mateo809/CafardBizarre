using System.Collections;
using PurrNet;
using PurrNet.Logging;
using PurrNet.Transports;
using UnityEngine;

#if UTP_LOBBYRELAY
using PurrNet.UTP;
using Unity.Services.Relay.Models;
#endif

namespace PurrLobby
{
    public class ConnectionStarter : MonoBehaviour
    {
        [Header("UDP Transport")]
        [SerializeField] private UDPTransport _udpTransport;

        [Header("Server IP (for client connections)")]
        [SerializeField] private string _serverIp = "127.0.0.1"; // IP du serveur
        private const ushort ServerPort = 5000;                    // Port fixe du serveur

        private NetworkManager _networkManager;
        private LobbyDataHolder _lobbyDataHolder;
        private bool _isFromLobby = false;

        private void Awake()
        {
            if (!TryGetComponent(out _networkManager))
            {
                PurrLogger.LogError($"Failed to get {nameof(NetworkManager)} component.", this);
            }

            _lobbyDataHolder = FindFirstObjectByType<LobbyDataHolder>();
            _isFromLobby = _lobbyDataHolder != null;
        }

        private void Start()
        {
            if (_udpTransport == null)
            {
                PurrLogger.LogError("UDPTransport is not assigned!", this);
                return;
            }

            _networkManager.transport = _udpTransport;

            // Configure l'adresse et le port du serveur pour le client
            _udpTransport.address = _serverIp;
            _udpTransport.serverPort = ServerPort;

            // Si propriétaire du lobby => serveur
            if (_lobbyDataHolder?.CurrentLobby.IsOwner == true)
            {
                _networkManager.StartServer(); // port fixe 5000
            }

#if UTP_LOBBYRELAY
            // Initialisation Relay si nécessaire
            if (_networkManager.transport is UTPTransport utp && _lobbyDataHolder != null)
            {
                if (_lobbyDataHolder.CurrentLobby.IsOwner)
                    utp.InitializeRelayServer((Allocation)_lobbyDataHolder.CurrentLobby.ServerObject);

                utp.InitializeRelayClient(_lobbyDataHolder.CurrentLobby.Properties["JoinCode"]);
            }
#endif

            // Démarre le client après un léger délai
            StartCoroutine(StartClientWithDelay());
        }

        private IEnumerator StartClientWithDelay()
        {
            yield return new WaitForSeconds(2f); // Attendre que le serveur soit prêt
            _networkManager.StartClient();       // UDPTransport choisira un port libre pour le client
        }
    }
}
