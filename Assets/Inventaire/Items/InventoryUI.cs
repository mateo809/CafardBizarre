using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PurrNet;

public class InventorySetSlot : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject slotPrefab;

    [Header("Sprite par défaut quand le slot est vide")]
    [SerializeField] private Sprite emptySprite;

    [Header("Recherche du parent UI")]
    [SerializeField] private string parentTag = "InventoryParent";
    [SerializeField] private string parentName = "ParentInventory";
    [SerializeField] private float retryDelay = 0.1f;
    [SerializeField] private int maxRetries = 50;

    private Transform _slotParent;
    private bool _initialized;

    protected override void OnSpawned()
    {
        base.OnSpawned();
        StartCoroutine(FindParentWithRetry());
    }

    private IEnumerator FindParentWithRetry()
    {
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            GameObject parentGO = GameObject.FindWithTag(parentTag) ?? GameObject.Find(parentName);

            if (parentGO != null && parentGO.activeInHierarchy)
            {
                _slotParent = parentGO.transform;
                _initialized = true;

                Debug.Log($"[InventorySetSlot] Parent trouvé pour {gameObject.name}");
                yield break;
            }

            retryCount++;
            yield return new WaitForSeconds(retryDelay);
        }

        Debug.LogError($"[InventorySetSlot] Parent introuvable pour {gameObject.name} !");
    }

    public void RefreshUI(List<InventorySlot> slots)
    {
        if (!_initialized || _slotParent == null)
        {
            Debug.LogWarning("[InventorySetSlot] Parent non initialisé, RefreshUI ignoré.");
            return;
        }

        if (slotPrefab == null)
        {
            Debug.LogError("[InventorySetSlot] slotPrefab est null.");
            return;
        }

        for (int i = _slotParent.childCount - 1; i >= 0; i--)
            Destroy(_slotParent.GetChild(i).gameObject);

        foreach (var slot in slots)
        {
            GameObject slotGO = Instantiate(slotPrefab, _slotParent);
            Image icon = slotGO.GetComponentInChildren<Image>();

            if (icon != null)
                icon.sprite = (slot.item != null && slot.item.visual != null) ? slot.item.visual : emptySprite;
        }
    }
}