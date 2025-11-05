using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class InventorySlot2
{
    public Image slotImage;  
    public Sprite itemSprite; 
}

public class InventoryUI : MonoBehaviour
{
    public InventorySlot2[] slots; 
    public Sprite emptySprite;    

    public Sprite[] items; 

    void Start()
    {
        UpdateInventory();
    }

    public void UpdateInventory()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < items.Length && items[i] != null)
            {
                slots[i].slotImage.sprite = items[i]; 
            }
            else
            {
                slots[i].slotImage.sprite = emptySprite; 
            }
        }
    }
}
