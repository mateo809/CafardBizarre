using PurrNet;
using PurrLobby;
using TMPro;
using UnityEngine;
#if STEAMWORKS_NET
using Steamworks;
#endif

public class PseudoManager : NetworkBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private GameObject pseudoPrefab;
    [SerializeField] private Vector3 offset = Vector3.up * 2f;
    [SerializeField] private float distanceThreshold = 50f;

    private TextMeshProUGUI _pseudoText;
    private Canvas _pseudoCanvas;
    private Camera _mainCamera;

    public SyncVar<string> playerPseudo = new SyncVar<string>("Player");

    protected override void OnSpawned()
    {
        base.OnSpawned();
        _mainCamera = Camera.main;

        if (isOwner)
        {
            string steamID = GetLocalSteamID();
            string pseudo = GetPlayerPseudoFromLobbyByID(steamID);
            SendPseudoToServerRPC(pseudo);
        }
    }

    private void Start()
    {
        if (pseudoPrefab != null)
        {
            GameObject pseudoUI = Instantiate(pseudoPrefab);
            _pseudoCanvas = pseudoUI.GetComponent<Canvas>();
            _pseudoText = pseudoUI.GetComponentInChildren<TextMeshProUGUI>();

            if (_pseudoCanvas != null)
            {
                _pseudoCanvas.renderMode = RenderMode.WorldSpace;
                _pseudoCanvas.transform.SetParent(transform);
                _pseudoCanvas.transform.localPosition = offset;
                _pseudoCanvas.transform.localRotation = Quaternion.identity;
            }
        }
    }

    private void LateUpdate()
    {
        if (_pseudoText != null && !string.IsNullOrEmpty(playerPseudo.value))
        {
            _pseudoText.text = playerPseudo.value;
        }

        if (_pseudoCanvas == null || _mainCamera == null) return;

        if (isOwner)
        {
            _pseudoCanvas.gameObject.SetActive(false);
            return;
        }

        float distanceToPlayer = Vector3.Distance(_mainCamera.transform.position, transform.position);
        if (distanceToPlayer > distanceThreshold)
        {
            _pseudoCanvas.gameObject.SetActive(false);
            return;
        }

        _pseudoCanvas.gameObject.SetActive(true);

        // Faire regarder le texte vers la caméra
        Vector3 directionToCamera = _mainCamera.transform.position - _pseudoCanvas.transform.position;
        _pseudoCanvas.transform.rotation = Quaternion.LookRotation(directionToCamera);

        // Ajouter une rotation de 180° sur l'axe Y pour que le texte soit dans le bon sens
        _pseudoCanvas.transform.Rotate(0, 180f, 0, Space.Self);
    }

    [ServerRpc]
    private void SendPseudoToServerRPC(string pseudo)
    {
        SyncPseudoToAllClientsRPC(pseudo);
    }

    [ObserversRpc]
    private void SyncPseudoToAllClientsRPC(string pseudo)
    {
        playerPseudo.value = pseudo;
        Debug.Log($"[PseudoManager] Pseudo synchronisé: {pseudo}");
    }

    private string GetLocalSteamID()
    {
#if STEAMWORKS_NET
        try
        {
            var steamID = SteamUser.GetSteamID();
            string id = steamID.m_SteamID.ToString();
            Debug.Log($"[PseudoManager] Local Steam ID: {id}");
            return id;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PseudoManager] Error getting Steam ID: {ex.Message}");
        }
#endif
        return "0";
    }

    private string GetPlayerPseudoFromLobbyByID(string steamID)
    {
        LobbyManager lobbyManager = FindObjectOfType<LobbyManager>();

        if (lobbyManager == null)
        {
            Debug.LogWarning("[PseudoManager] LobbyManager not found.");
            return "Player";
        }

        var currentLobby = lobbyManager.CurrentLobby;

        if (!currentLobby.IsValid || currentLobby.Members == null || currentLobby.Members.Count == 0)
        {
            Debug.LogWarning("[PseudoManager] Current lobby is invalid or has no members.");
            return "Player";
        }

        Debug.Log($"[PseudoManager] Looking for Steam ID: {steamID}");
        Debug.Log($"[PseudoManager] Lobby members count: {currentLobby.Members.Count}");

        for (int i = 0; i < currentLobby.Members.Count; i++)
        {
            var member = currentLobby.Members[i];
            Debug.Log($"[PseudoManager] [{i}] ID: {member.Id}, Name: {member.DisplayName}");
        }

        foreach (var member in currentLobby.Members)
        {
            if (member.Id == steamID)
            {
                Debug.Log($"[PseudoManager]  Found matching member: {member.DisplayName}");
                return member.DisplayName;
            }
        }

        Debug.LogWarning($"[PseudoManager] Steam ID {steamID} not found in lobby members.");
        return "Player";
    }

    private void OnDestroy()
    {
        if (_pseudoCanvas != null)
        {
            Destroy(_pseudoCanvas.gameObject);
        }
    }
}