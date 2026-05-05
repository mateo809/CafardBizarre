using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkinSelectionUI : MonoBehaviour
{
    [Header("Database")]
    [SerializeField] private SkinDatabase _skinDatabase;

    [Header("UI References")]
    [SerializeField] private Transform _gridParent;
    [SerializeField] private GameObject _skinSlotPrefab;
    [SerializeField] private Button _applyButton;
    [SerializeField] private TextMeshProUGUI _selectedNameLabel;
    [SerializeField] private GameObject _lockedMessage;

    [Header("PlayerRef")]
    [SerializeField] private PlayerSkinAttachment _localPlayerSkin;

    public static string CurrentSelectedSkinId { get; private set; }

    private const string SELECTED_SKIN_PREF = "SelectedSkinId";
    private readonly List<SkinSlotUI> _slots = new();

    private void Awake()
    {
        CurrentSelectedSkinId = PlayerPrefs.GetString(SELECTED_SKIN_PREF, "");

        if (_applyButton != null)
            _applyButton.onClick.AddListener(OnApplyClicked);
    }

    private void OnEnable()
    {
        BuildSlots();
    }

    private void OnDisable()
    {
        if (_applyButton != null)
            _applyButton.onClick.RemoveListener(OnApplyClicked);
    }

    private void BuildSlots()
    {
        foreach (var slot in _slots)
        {
            if (slot != null)
                Destroy(slot.gameObject);
        }

        _slots.Clear();

        if (_skinDatabase == null || _skinSlotPrefab == null || _gridParent == null)
        {
            Debug.LogWarning("[SkinSelectionUI] Missing references.");
            return;
        }

        foreach (var skin in _skinDatabase.GetAllSkins())
        {
            if (skin == null) continue;

            GameObject go = Instantiate(_skinSlotPrefab, _gridParent);
            go.SetActive(true);

            SkinSlotUI slotUI = go.GetComponent<SkinSlotUI>();
            if (slotUI == null)
            {
                Debug.LogError("[SkinSelectionUI] Le prefab SkinSlot n'a pas de composant SkinSlotUI !");
                Destroy(go);
                continue;
            }

            slotUI.Setup(skin, this);
            _slots.Add(slotUI);
        }

        RefreshAllSlots();
        UpdateSelectedLabel();
    }

    public void SelectSkin(string skinId)
    {
        if (_skinDatabase != null && _skinDatabase.GetSkinById(skinId) == null)
        {
            Debug.LogWarning($"[SkinSelectionUI] ID invalide : {skinId}");
            return;
        }

        CurrentSelectedSkinId = skinId;
        RefreshAllSlots();
        UpdateSelectedLabel();

        if (_lockedMessage != null)
            _lockedMessage.SetActive(false);

        Debug.Log($"[SkinSelectionUI] Sélectionné : {skinId}");
    }

    public void OnLockedSkinClicked(SkinData skin)
    {
        if (_lockedMessage != null)
            _lockedMessage.SetActive(true);

        if (_selectedNameLabel != null && skin != null)
            _selectedNameLabel.text = $"{skin.skinName} — Verrouillé";
    }

    private void OnApplyClicked()
    {
        if (string.IsNullOrEmpty(CurrentSelectedSkinId))
            return;

        PlayerPrefs.SetString(SELECTED_SKIN_PREF, CurrentSelectedSkinId);
        PlayerPrefs.Save();

        if (_localPlayerSkin != null)
            _localPlayerSkin.ApplySkin(CurrentSelectedSkinId);
        else
            Debug.LogWarning("[SkinSelectionUI] _localPlayerSkin non assigné — skin sauvegardé mais pas appliqué en live.");

        Debug.Log($"[SkinSelectionUI] Skin appliqué : {CurrentSelectedSkinId}");
    }

    private void RefreshAllSlots()
    {
        foreach (var slot in _slots)
        {
            if (slot != null)
                slot.Refresh();
        }
    }

    private void UpdateSelectedLabel()
    {
        if (_selectedNameLabel == null) return;

        if (string.IsNullOrEmpty(CurrentSelectedSkinId))
        {
            _selectedNameLabel.text = "Aucun skin sélectionné";
            return;
        }

        if (_skinDatabase == null)
        {
            _selectedNameLabel.text = CurrentSelectedSkinId;
            return;
        }

        var skin = _skinDatabase.GetSkinById(CurrentSelectedSkinId);
        _selectedNameLabel.text = skin != null ? skin.skinName : CurrentSelectedSkinId;
    }

    public void SetLocalPlayer(PlayerSkinAttachment attachment)
    {
        _localPlayerSkin = attachment;
    }
}