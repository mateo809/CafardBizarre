using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    public int _itemId;
    public string _itemName;
    public Sprite _icon;
    public float _weight;
    public float _minValue;
    public float _maxValue;
}

public static class ItemDatabase
{
    private static Dictionary<int, ItemData> _dict;

    public static void Init(ItemData[] allItems)
    {
        _dict = allItems.ToDictionary(i => i._itemId, i => i);
    }

    public static ItemData Get(int id) => _dict[id];
}