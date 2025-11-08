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

    private Transform slotParent;
    private bool _initialized = false;

    private void Awake()
    {
        if (isOwner)
        {
            StartCoroutine(FindParentWithRetry());
        }
    }

    private IEnumerator FindParentWithRetry()
    {
        int maxRetries = 50; 
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            GameObject parentGO = GameObject.FindWithTag("InventoryParent");

            if (parentGO == null)
            {
                parentGO = GameObject.Find("ParentInventory");
            }

            if (parentGO != null && parentGO.activeInHierarchy)
            {
                slotParent = parentGO.transform;
                _initialized = true;

                if (slotPrefab != null)
                {
                    Image img = slotPrefab.GetComponent<Image>();
                    if (img != null)
                        img.sprite = emptySprite;
                }

                Debug.Log($"[InventorySetSlot] Parent trouvé après {retryCount + 1} tentative(s) pour {gameObject.name}");
                yield break;
            }

            retryCount++;
            yield return new WaitForSeconds(0.1f);
        }

        Debug.LogError($"[InventorySetSlot] Parent introuvable après 5 secondes pour {gameObject.name}! Vérifie que 'ParentInventory' a le tag 'InventoryParent' ou existe dans la scène.");
    }

    public void RefreshUI(List<InventorySlot> slots)
    {
        // Seulement le OWNER met à jour son UI
        if (!isOwner)
        {
            Debug.LogWarning($"[InventorySetSlot] RefreshUI appelé par un non-owner sur {gameObject.name}. Ignoré.");
            return;
        }

        if (!_initialized || slotParent == null)
        {
            Debug.LogWarning("[InventorySetSlot] Parent non initialisé, RefreshUI ignoré.");
            return;
        }

        // Détruire les anciens slots
        foreach (Transform child in slotParent)
        {
            Destroy(child.gameObject);
        }

        // Créer les nouveaux slots
        foreach (var slot in slots)
        {
            GameObject slotGO = Instantiate(slotPrefab, slotParent);
            Image icon = slotGO.GetComponentInChildren<Image>();

            if (slot.item != null && slot.item.visual != null)
            {
                if (icon != null)
                    icon.sprite = slot.item.visual;
            }
            else
            {
                if (icon != null)
                    icon.sprite = emptySprite;
            }
        }
    }
}