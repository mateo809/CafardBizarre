using UnityEngine;


public interface IStackable
{
    public bool stackable { get; set; }
    public int maxStack { get; set; }
}

public interface IRenderable<VisualType>
{
    public VisualType visual { get; set; }
}

public interface IItem : IStackable, IRenderable<Sprite>
{
    public string itemName { get; set; }
}

[CreateAssetMenu(fileName = "Item", menuName = "Items/Item")]
public class Item : ScriptableObject, IItem
{
    [SerializeField] private string _itemName;
    [SerializeField] private bool _stackable;
    [SerializeField] private int _maxStack;
    [SerializeField] private Sprite _sprite;
    [SerializeField] private int _itemWeight;
    [SerializeField] private int _price;

    public string itemName { get => _itemName; set => _itemName = value; }  
    public bool stackable { get => _stackable; set => _stackable = value; }
    public int maxStack { get => _maxStack; set => _maxStack = value; }
    public Sprite visual { get => _sprite; set => _sprite = value; }

    public int priceItem { get => _price; set => _price = value; }
    public int itemWeight { get => _itemWeight; set => _itemWeight = value; }
}