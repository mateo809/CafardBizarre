using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ItemSlotView
{
    private ItemSlotData data;
    private Image itemImage;
    private TextMeshProUGUI numberText;

    public ItemSlotView(ItemSlotData data, Image itemImage, TextMeshProUGUI numberText)
    {
        this.data = data;
        this.itemImage = itemImage;
        this.numberText = numberText;
        Update();
    }

    public void Update()
    {
        Item currentItem = data.GetItemContained();

        if (currentItem == data.GetDefaultItem())
        {
            // Si c'est le slot vide, on cache l'image et le texte
            itemImage.enabled = false;
            numberText.text = "";
        }
        else
        {
            // Sinon, on affiche le sprite et le nombre
            itemImage.enabled = true;
            itemImage.sprite = currentItem.visual; // <- ici on prend le sprite depuis le ScriptableObject
            numberText.text = data.GetNumber() > 1 ? data.GetNumber().ToString() : "";
        }
    }
}
