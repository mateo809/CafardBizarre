using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryLine : MonoBehaviour
{

    public bool _isLocked = true;

    [SerializeField] private GameObject _player;
    public List<ItemData> inventoryLinesOfItem = new List<ItemData>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        IsLocked();
    }

    public void UnlockAnInventoryLine()
    {
        this._isLocked = false;
    }

    public bool IsFull()
    {
        if (inventoryLinesOfItem.Count < 4)
        {
            return false;
        }
        else
        {
            return true;

        }
    }
    public bool IsLocked()
    {

        if (_isLocked)
        {
            this.GetComponent<Image>().color = Color.darkGray;
            return true;
        } else
        {
            this.GetComponent<Image>().color = Color.darkBlue;
            return false;
        }

    }

    public void DropItem(int index  )
    {
        ItemData itemDataToDrop = inventoryLinesOfItem[index];
        ItemToPickup itemTP = new ItemToPickup(itemDataToDrop, "Press \"E\" to pick up");
        
        itemTP = Instantiate(itemTP, _player.transform);

    }

    
}
