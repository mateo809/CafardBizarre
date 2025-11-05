using UnityEngine;

public class ItemPickUp : MonoBehaviour
{
    [Tooltip("L'asset Item (ScriptableObject) que cet objet représente.")]
    public Item itemData;

    public int amount = 1;

    public void Interact(GameObject interactor)
    {
        InventoryManager inventory = interactor.GetComponent<InventoryManager>();

        if (inventory != null)
        {
            if (itemData != null)
            {
                bool success = inventory.AddItem(itemData, amount);

                if (success)
                {
                    Debug.Log($"Picked up {itemData.itemName} x{amount} into inventory.");

                }
                else
                {
                    Debug.Log($"Inventory full or item cannot be added.");
                }
            }
            else
            {
                Debug.LogError("ItemPickUp is missing a reference to an Item ScriptableObject!");
            }
        }
        else
        {
            // Ceci est utile si l'objet interagit avec autre chose que le joueur
            Debug.LogWarning("Interactor does not have an InventoryManager component.");
        }
    }
}