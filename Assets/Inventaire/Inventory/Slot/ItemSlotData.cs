public class ItemSlotData
{
    private Item itemContained;
    private int number;
    private Item defaultItem;

    public ItemSlotData(Item defaultItem)
    {
        this.defaultItem = defaultItem;
        itemContained = defaultItem;
        number = 0;
    }

    public void SetItem(Item item, int number)
    {
        itemContained = item;
        this.number = number;
    }

    public void Reset()
    {
        itemContained = defaultItem;
        number = 0;
    }

    public Item GetItemContained() => itemContained;
    public int GetNumber() => number;
    public Item GetDefaultItem() => defaultItem;

    public bool TryAddItem(Item item, int numberToAdd)
    {
        if (itemContained == defaultItem || itemContained == item)
        {
            number += numberToAdd;
            return true;
        }
        return false;
    }
}
