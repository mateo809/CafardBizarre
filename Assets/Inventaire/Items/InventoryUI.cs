using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PurrNet;

public class InventorySetSlot : NetworkBehaviour
{
    public GameObject slotPrefab;

    [Header("Sprite par défaut quand le slot est vide")]
    public Sprite emptySprite;

    private Transform _slotParent;
    private bool _initialized = false;
    protected override void OnSpawned()
    {
        base.OnSpawned();

        if (isOwner)
            StartCoroutine(FindParentWithRetry());
    }

    private IEnumerator FindParentWithRetry()
    {
        int maxRetries = 50;
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            GameObject parentGO = GameObject.FindWithTag("InventoryParent")
                               ?? GameObject.Find("ParentInventory");

            if (parentGO != null && parentGO.activeInHierarchy)
            {
                _slotParent = parentGO.transform;
                _initialized = true;

                if (slotPrefab != null)
                {
                    Image img = slotPrefab.GetComponent<Image>();
                    if (img != null) img.sprite = emptySprite;
                }

                Debug.Log($"[InventorySetSlot] Parent trouvé après {retryCount + 1} tentative(s) pour {gameObject.name}");
                yield break;
            }

            retryCount++;
            yield return new WaitForSeconds(0.1f);
        }

        Debug.LogError($"[InventorySetSlot] Parent introuvable après 5 secondes pour {gameObject.name} ! " +
                       "Vérifie que 'ParentInventory' a le tag 'InventoryParent' ou existe dans la scène.");
    }

    public void RefreshUI(List<InventorySlot> slots)
    {
        if (!isOwner)
        {
            Debug.LogWarning($"[InventorySetSlot] RefreshUI appelé par un non-owner sur {gameObject.name}. Ignoré.");
            return;
        }

        if (!_initialized || _slotParent == null)
        {
            Debug.LogWarning("[InventorySetSlot] Parent non initialisé, RefreshUI ignoré.");
            return;
        }

        foreach (Transform child in _slotParent)
            Destroy(child.gameObject);

        foreach (var slot in slots)
        {
            GameObject slotGO = Instantiate(slotPrefab, _slotParent);
            Image icon = slotGO.GetComponentInChildren<Image>();
            if (icon != null)
                icon.sprite = (slot.item != null && slot.item.visual != null) ? slot.item.visual : emptySprite;
        }
    }
}