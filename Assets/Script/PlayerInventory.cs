using PurrNet;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class PlayerInventory : NetworkBehaviour
{
    public static readonly List<PlayerInventory> AllPlayers = new List<PlayerInventory>();


    [Header("UI")]
    public GameObject inventoryPrefab;

    [SerializeField] Image itemBorder;

    [SerializeField] Image itemIcon;

    public ItemData itemHolded = null;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        if (isOwner && inventoryPrefab != null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {

                Instantiate(inventoryPrefab, canvas.transform, false);



            }
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public bool PickupItem(ItemData itemData)
    {
        if (InventoryIsEmpty())
        {
            itemHolded = itemData;
            return true;
        }
        else
        {
            Debug.Log("inventory is full");
            return false;
        }

    }

    bool InventoryIsEmpty()
    {
        return itemHolded == true;
    }
}
