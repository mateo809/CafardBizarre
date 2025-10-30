using JetBrains.Annotations;
using UnityEngine;

public class ItemToPickup : MonoBehaviour
{

    public ItemData _itemData;

    public string _interactMessage;

    public ItemToPickup( ItemData itemData,  string interactMessage)
    {
        _itemData = itemData;
        _interactMessage = interactMessage;
    }

    private void Start()
    {
        
    }


}