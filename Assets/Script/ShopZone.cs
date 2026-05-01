using PurrNet;
using System.Collections.Generic;
using UnityEngine;

public class ShopZone : NetworkBehaviour
{
    [Header("Shop Settings")]
    [SerializeField] private GlobalEconomyManager _globalEconomyManager;

    private Dictionary<GameObject, int> _itemsInZone = new Dictionary<GameObject, int>();
    private HashSet<GameObject> _soldItems = new HashSet<GameObject>();

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer != LayerMask.NameToLayer("Item"))
            return;

        GameObject item = other.gameObject;

        if (_soldItems.Contains(item))
            return;

        ItemPickUp itemData = item.GetComponent<ItemPickUp>();
        if (itemData == null)
            return;

        int price = itemData.price;

        if (_itemsInZone.ContainsKey(item))
            return;

        _itemsInZone.Add(item, price);

        RequestUpdateScore(price);

        AudioController.Instance.PlaySound(AudioType.SellItem, AudioSourceType.Game);

        // marque comme vendu définitivement
        _soldItems.Add(item);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer != LayerMask.NameToLayer("Item"))
            return;

        GameObject item = other.gameObject;

        if (_itemsInZone.ContainsKey(item))
        {
            _itemsInZone.Remove(item);
        }
    }

    private void RequestUpdateScore(int delta)
    {
        if (isServer)
            UpdateScoreOnServer(delta);
        else
            UpdateScoreRPC(delta);
    }

    [ServerRpc]
    private void UpdateScoreRPC(int delta)
    {
        UpdateScoreOnServer(delta);
    }

    [ServerOnly]
    private void UpdateScoreOnServer(int delta)
    {
        _globalEconomyManager.totalMoney.value += delta;

        Debug.Log($" Score mis à jour: +{delta} | Total: {_globalEconomyManager.totalMoney.value}");
    }
}