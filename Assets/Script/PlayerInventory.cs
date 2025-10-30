using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerInventory : MonoBehaviour
{


    public GameObject _spawnLocation;


    private RoachController _ownerController;

    private List<InventoryLine> _inventory = new List<InventoryLine>();
    private List<ItemData> _items = new List<ItemData>();

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

    public void UpdateInventoryRender()
    {
        foreach (InventoryLine line in _inventory)
        {
            if (!line.IsLocked())
            {

                int i = 0;
                foreach (ItemData data in line.inventoryLinesOfItem)
                {

                    line.transform.GetChild(i).GetComponent<Image>().sprite = data._icon;
                    i++;
                }
            }
        }
    }

    public void SetInventoryVisibilityAtFalse()
    {
        _parentInventoryLines.SetActive(false);
    }
    public void SetInventoryVisibilityAtTrue()
    {
        _parentInventoryLines.SetActive(true);
    }

    public bool InventoryHasSpace(ItemData itemToPut)
    {
      

        if (_ownerController.currentHoldingWeight + itemToPut._weight >= _ownerController.maxHoldingWeight 
            && _ownerController.inventoryMaxSize >= _inventory.Count )
        {
            return false;
        }
        return true;
    }
    
    //If return 10 then there is a problem
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
    public bool PutInInventory(ItemData itemData)
    {
        try
        {
            if (InventoryHasSpace(itemData))
            {
                int i = WhichSlotInventory();
                Debug.Log("which slot : " + i);


                if ( i != 10) // if i == 10 then there is an error in WhichSlotInventory OR the inventory is full
                {
                    _inventory[i].inventoryLinesOfItem.Add(itemData);
                } else
                {
                    return false;
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
