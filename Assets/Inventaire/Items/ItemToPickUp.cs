using UnityEngine;

public class ItemPickUp : MonoBehaviour
{
    public Item itemData;
    public int amount = 1;
    public float weight = 1f;
    public int price = 10;

    public InventoryManager owner;
    public void Interact(GameObject interactor)
    {
        InventoryManager inventory = interactor.GetComponent<InventoryManager>();
        if (inventory != null && itemData != null)
        {
            bool success = inventory.AddItem(itemData, amount, price);
            if (success)
            {
                owner = inventory;
                Debug.Log($"Picked up {itemData.itemName} x{amount}.");
            }
        }
    }
}
