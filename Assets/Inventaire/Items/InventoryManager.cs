using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class InventorySlot
{
    public Item item;
    public int quantity;

    public InventorySlot(Item item, int quantity)
    {
        this.item = item;
        this.quantity = quantity;
    }

    public void AddQuantity(int amount)
    {
        quantity += amount;
    }
}

public class InventoryManager : MonoBehaviour
{
    public List<InventorySlot> inventorySlots = new List<InventorySlot>();
    public int maxSlots = 20;

    public InventorySetSlot inventoryUI;

    // --- Ajout d'objet ---
    public bool AddItem(Item itemToAdd, int amount = 1)
    {
        if (itemToAdd == null) return false;

        if (itemToAdd.stackable)
        {
            InventorySlot existingSlot = inventorySlots.FirstOrDefault(slot =>
                slot.item == itemToAdd && slot.quantity < itemToAdd.maxStack);

            if (existingSlot != null)
            {
                int canAdd = itemToAdd.maxStack - existingSlot.quantity;
                int actualAdded = Mathf.Min(amount, canAdd);

                existingSlot.AddQuantity(actualAdded);
                amount -= actualAdded;

                if (amount <= 0)
                {
                    Debug.Log($"Added {itemToAdd.itemName} x{actualAdded} to existing stack.");
                    inventoryUI.RefreshUI(inventorySlots); // Mise à jour UI
                    return true;
                }
            }
        }

        while (amount > 0)
        {
            if (inventorySlots.Count >= maxSlots)
            {
                Debug.Log("Inventory is full. Remaining items not added.");
                return false;
            }

            int qtyToAdd = itemToAdd.stackable ? Mathf.Min(amount, itemToAdd.maxStack) : 1;

            InventorySlot newSlot = new InventorySlot(itemToAdd, qtyToAdd);
            inventorySlots.Add(newSlot);

            amount -= qtyToAdd;
            Debug.Log($"Added new slot for {itemToAdd.itemName} x{qtyToAdd}.");
        }

        inventoryUI.RefreshUI(inventorySlots); // Mise à jour UI
        return true;
    }

    // --- Retrait d'objet ---
    public bool RemoveItem(Item itemToRemove, int amount = 1)
    {
        InventorySlot slot = inventorySlots.FirstOrDefault(s => s.item == itemToRemove);

        if (slot == null)
        {
            Debug.Log($"Item {itemToRemove.itemName} not found in inventory.");
            return false;
        }

        if (slot.quantity > amount)
        {
            slot.quantity -= amount;
            Debug.Log($"Removed {itemToRemove.itemName} x{amount}. Remaining: {slot.quantity}");
        }
        else
        {
            amount -= slot.quantity;
            inventorySlots.Remove(slot);
            Debug.Log($"Removed last stack of {itemToRemove.itemName}.");
        }

        inventoryUI.RefreshUI(inventorySlots); // Mise à jour UI
        return true;
    }

    public float GetTotalWeight()
    {
        float totalWeight = 0f;
        foreach (var slot in inventorySlots)
        {
            totalWeight += slot.item.itemWeight * slot.quantity;
        }
        return totalWeight;
    }
}
