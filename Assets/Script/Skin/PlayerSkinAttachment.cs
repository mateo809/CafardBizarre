using UnityEngine;
using PurrNet;

public class PlayerSkinAttachment : NetworkBehaviour
{
    [Header("Anchor")]
    [SerializeField] private Transform _skinAnchor;

    [Header("Database")]
    [SerializeField] private SkinDatabase _skinDatabase;

    [Header("Backend")]
    [SerializeField] private BackendCaller _backendCaller;

    private GameObject _currentSkinInstance;
    private string _currentSkinId;

    private void Awake()
    {
        if (_backendCaller == null)
            _backendCaller = GetComponent<BackendCaller>();
    }

    public void ApplySkin(string skinId)
    {
        if (string.IsNullOrWhiteSpace(skinId)) return;
        if (_currentSkinId == skinId) return;

        if (_skinDatabase == null || _skinAnchor == null)
        {
            Debug.LogWarning("[PlayerSkin] Missing database or anchor.");
            return;
        }

        SkinData skin = _skinDatabase.GetSkinById(skinId);
        if (skin == null)
        {
            Debug.LogWarning($"[PlayerSkin] Skin introuvable : {skinId}");
            return;
        }

        if (!SkinUnlockManager.Instance.IsSkinUnlocked(skin))
        {
            Debug.LogWarning($"[PlayerSkin] Skin verrouillé : {skinId}");
            return;
        }

        ApplySkinLocal(skin);

        if (isOwner)
            RPC_ApplySkinOnServer(skinId);

        if (_backendCaller != null && int.TryParse(skin.unlockId, out int unlockId))
            StartCoroutine(_backendCaller.EquipCosmetic(unlockId, skin.skinName));
    }

    private void ApplySkinLocal(SkinData skin)
    {
        if (_currentSkinInstance != null)
            Destroy(_currentSkinInstance);

        if (skin.skinPrefab == null)
            return;

        _currentSkinInstance = Instantiate(skin.skinPrefab, _skinAnchor);
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
        if (isOwner) return;

        SkinData skin = _skinDatabase.GetSkinById(skinId);
        if (skin != null)
            ApplySkinLocal(skin);
    }

    public string GetCurrentSkinId() => _currentSkinId;
}