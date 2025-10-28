using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryLine : MonoBehaviour
{

    public bool _isLocked = true;

    public List<ItemToPickup> inventoryLinesOfItem = new List<ItemToPickup>(4);

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
            return true;
        }
        else
        {
            return false;

        }
    }
    public void IsLocked()
    {

        if (_isLocked)
        {
            this.GetComponent<Image>().color = Color.darkGray;
        } else
        {
            this.GetComponent<Image>().color = Color.darkBlue;
        }

    }

    internal void SetParent(GameObject parentInventoryLines)
    {
        throw new NotImplementedException();
    }
}
