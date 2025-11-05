using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventorySetSlot : MonoBehaviour
{
    public GameObject slotPrefab;

    [Header("Sprite par défaut quand le slot est vide")]
    public Sprite emptySprite;

    private Transform slotParent;

    private void Awake()
    {
        string parentName = "ParentInventory";
        GameObject parentGO = GameObject.Find(parentName);

        if (parentGO != null)
        {
            slotParent = parentGO.transform;
        }
        else
        {
            Debug.LogError($"Transform '{parentName}' introuvable dans la scène !");
        }

        slotPrefab.GetComponent<Image>().sprite = emptySprite;

    }


    public void RefreshUI(List<InventorySlot> slots)
    {
        if (slotParent == null) return;

        foreach (Transform child in slotParent)
        {
            Destroy(child.gameObject);
        }

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
