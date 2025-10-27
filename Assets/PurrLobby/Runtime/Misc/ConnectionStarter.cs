using System.Collections;
using PurrNet;
using PurrNet.Logging;
using PurrNet.Transports;
using Steamworks;
using UnityEngine;

#if UTP_LOBBYRELAY
using PurrNet.UTP;
using Unity.Services.Relay.Models;
#endif

namespace PurrLobby
{
    public class ConnectionStarter : MonoBehaviour
    {
        [Header("Optional overrides")]
        [SerializeField] private PurrTransport _transport;
        [SerializeField] private UDPTransport _udpTransport;

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
            if (_lobbyDataHolder)
                _isFromLobby = true;
        }

        private void Start()
        {
            // 🔹 Sécurité : si le NetworkManager a déjà un transport, on le récupère
            if (_networkManager.transport == null)
            {
                if (_networkManager.transport == null)
                {
                    PurrLogger.LogError(" No transport found! Please assign one to the NetworkManager.", this);
                    return;
                }
                else
                {
                    PurrLogger.Log($" Found transport: {_networkManager.transport.GetType().Name}", this);
                }
            }

            if (_isFromLobby)
                StartFromLobby();
            else
                StartNormal();

            // Applique le nom de lobby s’il y a un PurrTransport
            if (_networkManager.transport is PurrTransport purrTransport && _lobbyDataHolder != null)
            {
                purrTransport.roomName = _lobbyDataHolder.CurrentLobby.LobbyId;
            }

#if UTP_LOBBYRELAY
            else if(_networkManager.transport is UTPTransport utp)
            {
                if(_lobbyDataHolder.CurrentLobby.IsOwner)
                {
                    utp.InitializeRelayServer((Allocation)_lobbyDataHolder.CurrentLobby.ServerObject);
                }
                utp.InitializeRelayClient(_lobbyDataHolder.CurrentLobby.Properties["JoinCode"]);
            }
#else
            // P2P fallback
#endif

            if (_lobbyDataHolder?.CurrentLobby.IsOwner == true)
                _networkManager.StartServer();

            StartCoroutine(StartClient());
        }

        private void StartNormal()
        {
            // Utilise le transport UDP par défaut si assigné
            if (_udpTransport != null)
                _networkManager.transport = _udpTransport;

            _networkManager.StartServer();
            StartCoroutine(StartClient());
        }

        private void StartFromLobby()
        {
            if (_transport != null)
                _networkManager.transport = _transport;

            if (!_lobbyDataHolder)
            {
                PurrLogger.LogError($"Failed to start connection. {nameof(LobbyDataHolder)} is null!", this);
                return;
            }

            if (!_lobbyDataHolder.CurrentLobby.IsValid)
            {
                PurrLogger.LogError($"Failed to start connection. Lobby is invalid!", this);
                return;
            }
        }

        private IEnumerator StartClient()
        {
            yield return new WaitForSeconds(1f);
            _networkManager.StartClient();
        }
    }
}
