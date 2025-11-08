using PurrNet;
using UnityEngine;

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

            RequestSellItem(playerInventory.GetSlot(0).price);
        }
    }

    private void RequestSellItem(int itemPrice)
    {
        if (isServer)
        {
            ProcessSellOnServer(itemPrice);
        }
        else
        {
            ProcessSellOnServerRPC(itemPrice);
        }
    }

    [ServerOnly]
    private void ProcessSellOnServerRPC(int itemPrice)
    {
        ProcessSellOnServer(itemPrice);
    }

    private void ProcessSellOnServer(int itemPrice)
    {
        if (itemPrice < 0)
        {
            Debug.LogWarning("Tentative de vente avec un prix invalide!");
            return;
        }

        BroadcastSellToAllClients(itemPrice);
    }

    [ServerRpc]
    private void BroadcastSellToAllClients(int itemPrice)
    {
        _globalEconomyManager.totalMoney.value += itemPrice;
        Debug.Log($" Sold item for {itemPrice} coins. Total money: {_globalEconomyManager.totalMoney.value}");

        if (roachController != null)
            roachController.DropItem();
    }
}