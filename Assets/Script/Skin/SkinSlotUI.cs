// SkinSlotUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkinSlotUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image _previewIcon;
    [SerializeField] private TextMeshProUGUI _skinNameText;
    [SerializeField] private GameObject _lockedOverlay;
    [SerializeField] private GameObject _selectedOutline;
    [SerializeField] private Button _button;

    private SkinData _skinData;
    private SkinSelectionUI _parentMenu;

    public void Setup(SkinData skin, SkinSelectionUI parentMenu)
    {
        _skinData = skin;
        _parentMenu = parentMenu;

        if (_previewIcon != null)
        {
            _previewIcon.sprite = skin.previewIcon;
            _previewIcon.enabled = skin.previewIcon != null;
        }

        if (_skinNameText != null)
            _skinNameText.text = skin.skinName;

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(OnClick);

        // Cacher le slot entier si le skin n'est pas débloqué
        bool unlocked = SkinUnlockManager.Instance.IsSkinUnlocked(skin);
        gameObject.SetActive(unlocked);

        Refresh();
    }

    public void Refresh()
    {
        if (_skinData == null) return;

        bool unlocked = SkinUnlockManager.Instance.IsSkinUnlocked(_skinData);
        bool selected = SkinSelectionUI.CurrentSelectedSkinId == _skinData.skinId;

        gameObject.SetActive(unlocked);

        if (_lockedOverlay != null)
            _lockedOverlay.SetActive(false); // Plus utile, le slot est caché si locked

        if (_selectedOutline != null)
            _selectedOutline.SetActive(selected);

        _button.interactable = true;
    }

    private void OnClick()
    {
        if (_skinData == null) return;
        _parentMenu?.SelectSkin(_skinData.skinId);
    }


}