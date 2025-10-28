using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{

   

    private RoachController _ownerController;

    private List<InventoryLine> _inventory = new List<InventoryLine>(4);

    [SerializeField] private InventoryLine _prefabInventoryLine;
    [SerializeField] private GameObject _parentInventoryLines;

    private void Awake()
    {
        

        _ownerController = gameObject.GetComponent<RoachController>();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        for (int i = 0; i < 4; i++)
        {
            InventoryLine newInventoryLine = Instantiate(_prefabInventoryLine);
            newInventoryLine.transform.SetParent(_parentInventoryLines.transform);
            _inventory.Add(newInventoryLine);
        }
        
        _inventory[0].UnlockAnInventoryLine();
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetInventoryVisibilityAtFalse()
    {
        _parentInventoryLines.SetActive(false);
    }
    public void SetInventoryVisibilityAtTrue()
    {
        _parentInventoryLines.SetActive(true);
    }

    public bool InventoryHasSpace(ItemToPickup itemToPut)
    {

        if (_ownerController.currentHoldingWeight + itemToPut.weight >= _ownerController.maxHoldingWeight 
            && _ownerController.inventoryMaxSize >= _inventory.Count )
        {
            return false;
        }
        return true;
    }
    
    //If return 100 then there is a problem
    private int WhichSlotInventory()
    {
        for (int i = 0; i < _inventory.Count; i++)
        {
            if (!_inventory[i]._isLocked && !_inventory[i].IsFull())
            {
                return i;
            }
        }
        return 10;
    }

    // If return false : an error has occured
    // If return true : everything is good
    public bool PutInInventory(ItemToPickup itemToPut)
    {
        try
        {
            if (InventoryHasSpace(itemToPut))
            {
                int i = WhichSlotInventory();
                if ( i != 10)
                {
                    _inventory[i].inventoryLinesOfItem.Add(itemToPut);
                }
            }
            return true;
        }
        catch(Exception e)
        {
            Debug.LogError("The item can't be put inside the inventory because" + e);
            return false;
        }
        }

}
