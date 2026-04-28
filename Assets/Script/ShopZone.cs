using NUnit.Framework.Interfaces;
using PurrNet;
using System.Collections.Generic;
using UnityEngine;

public class ShopZone : NetworkBehaviour
{
    [Header("Shop Settings")]
    [SerializeField] GlobalEconomyManager _globalEconomyManager;

    private Dictionary<GameObject, int> _itemsInZone = new Dictionary<GameObject, int>();

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer != LayerMask.NameToLayer("Item")) return;

        ItemPickUp itemData = other.GetComponent<ItemPickUp>();
        if (itemData == null) return;

        int price = itemData.price;

        if (!_itemsInZone.ContainsKey(other.gameObject))
        {
            _itemsInZone.Add(other.gameObject, price);
            RequestUpdateScore(price);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer != LayerMask.NameToLayer("Item")) return;

        if (_itemsInZone.TryGetValue(other.gameObject, out int price))
        {
            _itemsInZone.Remove(other.gameObject);
            RequestUpdateScore(-price);
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
        Debug.Log($"Score mis à jour: +{delta} ? Total: {_globalEconomyManager.totalMoney.value}");
    }
}