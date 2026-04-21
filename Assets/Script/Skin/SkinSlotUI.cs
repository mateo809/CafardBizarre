using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// À placer sur chaque slot de skin dans le menu cosmétique.
/// Affiche l'icône, le nom, l'état (verrouillé/sélectionné) et gère le clic.
/// </summary>
public class SkinSlotUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image _previewIcon;
    [SerializeField] private TextMeshProUGUI _skinNameText;
    [SerializeField] private GameObject _lockedOverlay;      // Objet affiché si verrouillé
    [SerializeField] private GameObject _selectedOutline;    // Outline/highlight si sélectionné
    [SerializeField] private Button _button;

    private SkinData _skinData;
    private SkinSelectionUI _parentMenu;

    /// <summary>Initialise le slot avec les données du skin.</summary>
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

        Refresh();
    }

    /// <summary>Met à jour l'affichage (locked/selected) sans recréer le slot.</summary>
    public void Refresh()
    {
        if (_skinData == null) return;

        bool unlocked = SkinUnlockManager.Instance.IsSkinUnlocked(_skinData);
        bool selected = SkinSelectionUI.CurrentSelectedSkinId == _skinData.skinId;

        if (_lockedOverlay != null)
            _lockedOverlay.SetActive(!unlocked);

        if (_selectedOutline != null)
            _selectedOutline.SetActive(selected);

        _button.interactable = true; // Le clic est toujours possible (affichage du statut)
    }

    private void OnClick()
    {
        if (_skinData == null) return;

        bool unlocked = SkinUnlockManager.Instance.IsSkinUnlocked(_skinData);

        if (!unlocked)
        {
            Debug.Log($"[SkinSlotUI] Skin verrouillé : {_skinData.skinName}");
            // Tu peux déclencher ici une animation de "shake" ou un popup
            _parentMenu?.OnLockedSkinClicked(_skinData);
            return;
        }

        _parentMenu?.SelectSkin(_skinData.skinId);
    }
}