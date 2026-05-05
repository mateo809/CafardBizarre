using UnityEngine;
using PurrNet;

/// <summary>
/// Composant à placer sur le prefab joueur.
/// Gère l'instanciation et l'attachement du skin 3D
/// sur le Transform désigné (ancre cosmetique).
/// </summary>
public class PlayerSkinAttachment : NetworkBehaviour
{
    [Header("Anchor")]
    [Tooltip("Le Transform sur le prefab joueur où le skin sera attaché")]
    [SerializeField] private Transform _skinAnchor;

    [Header("Database")]
    [SerializeField] private SkinDatabase _skinDatabase;
    private BackendCaller _backendCaller;

    // Skin actuellement instancié
    private GameObject _currentSkinInstance;
    private string _currentSkinId;

    /// <summary>
    /// Applique le skin localement (appelé au spawn ou depuis le menu).
    /// En réseau, utilise un ServerRpc / ObserversRpc pour synchroniser.
    /// </summary>
    public void ApplySkin(string skinId)
    {
        if (_currentSkinId == skinId) return;

        SkinData skin = _skinDatabase.GetSkinById(skinId);
        if (skin == null)
        {
            Debug.LogWarning($"[PlayerSkin] Skin introuvable : {skinId}");
            return;
        }

        // Vérifie si déverrouillé
        if (!SkinUnlockManager.Instance.IsSkinUnlocked(skin))
        {
            Debug.LogWarning($"[PlayerSkin] Skin verrouillé : {skinId}");
            return;
        }

        ApplySkinLocal(skin);

        // Si on est owner, on synchronise aux autres joueurs
        if (isOwner)
            RPC_ApplySkinOnServer(skinId);

        _backendCaller.GetComponent<BackendCaller>();
        Debug.LogWarning("TEST BACKEND !!!!!!!");
        if (_backendCaller != null)
            StartCoroutine(_backendCaller.EquipCosmetic(int.Parse(skin.unlockId), skin.skinName));
    }

    private void ApplySkinLocal(SkinData skin)
    {
        // Détruit l'ancien skin
        if (_currentSkinInstance != null)
            Destroy(_currentSkinInstance);

        if (skin.skinPrefab == null) return;

        // Instancie sur l'ancre
        _currentSkinInstance = Instantiate(skin.skinPrefab, _skinAnchor);

        // Applique les overrides de transform
        _currentSkinInstance.transform.localPosition = skin.positionOffset;
        _currentSkinInstance.transform.localEulerAngles = skin.rotationOffset;
        _currentSkinInstance.transform.localScale = skin.scaleOverride;

        _currentSkinId = skin.skinId;
    }

    [ServerRpc(requireOwnership: true)]
    private void RPC_ApplySkinOnServer(string skinId)
    {
        RPC_ApplySkinOnClients(skinId);
    }

    [ObserversRpc(bufferLast: true)]
    private void RPC_ApplySkinOnClients(string skinId)
    {
        // Évite de re-appliquer sur l'owner (déjà fait localement)
        if (isOwner) return;

        SkinData skin = _skinDatabase.GetSkinById(skinId);
        if (skin != null) ApplySkinLocal(skin);
    }

    /// <summary>Retourne le skinId actuellement équipé.</summary>
    public string GetCurrentSkinId() => _currentSkinId;
}