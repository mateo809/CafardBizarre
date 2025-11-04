using JetBrains.Annotations;
using UnityEngine;

public class ItemToPickup : MonoBehaviour
{

    public ItemData _itemData;

    

    public ItemToPickup(ItemData itemData, string interactMessage)
    {
        _itemData = itemData;
        
    }

    private void Start()
    {

    }



}