using UnityEngine;
using PurrNet;
using System.Runtime.InteropServices;

public class ShopZone : NetworkBehaviour
{
    [Header("Shop Settings")]

    private RoachController roachController;

    [SerializeField] GlobalEconomyManager _globalEconomyManager;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Item"))
        {
            Destroy(other.gameObject);
            return; 
        }

        InventoryManager playerInventory = other.GetComponent<InventoryManager>();
        roachController = other.GetComponent<RoachController>();
        if (playerInventory != null)
        {
            Debug.Log(playerInventory.GetSlot(0).item.priceItem);
            Debug.Log(playerInventory.GetSlot(0).price);
            SellAllItems(playerInventory);
        }
    }


    private void SellAllItems(InventoryManager playerInventory)
    {
        
        int itemPrice = playerInventory.GetSlot(0).price;

       _globalEconomyManager.totalMoney.value += itemPrice;

        Debug.Log($"Sold 1 x {playerInventory.GetSlot(0).item.itemName} for {itemPrice} coins.");

        if (roachController != null)
            roachController.DropItem();

    }
}
