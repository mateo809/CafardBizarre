using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [Header("References")]
    public Slider healthSlider;       // Glisse ton slider ici
    public Image fillImage;           // Glisse l'image Fill ici
    public Image stateImage;          // Image qui change selon l'état de la vie

    [Header("State Sprites")]
    public Sprite fullHealthSprite;
    public Sprite mediumHealthSprite;
    public Sprite lowHealthSprite;

    [Header("Health Colors")]
    public Color fullHealthColor = Color.green;
    public Color mediumHealthColor = Color.yellow;
    public Color lowHealthColor = Color.red;

    // Appelle cette fonction pour mettre à jour la santé
    public void SetHealth(float currentHealth, float maxHealth)
    {
        if (healthSlider == null) return;

        healthSlider.maxValue = maxHealth;
        healthSlider.value = currentHealth;

        UpdateFillColor(currentHealth, maxHealth);
        UpdateStateImage(currentHealth, maxHealth);
    }

    private void UpdateFillColor(float currentHealth, float maxHealth)
    {
        if (fillImage == null) return;

        float ratio = currentHealth / maxHealth;

        if (ratio > 0.5f)
            fillImage.color = fullHealthColor;
        else if (ratio > 0.25f)
            fillImage.color = mediumHealthColor;
        else
            fillImage.color = lowHealthColor;
    }

    private void UpdateStateImage(float currentHealth, float maxHealth)
    {
        if (stateImage == null) return;

        float ratio = currentHealth / maxHealth;

        if (ratio > 0.5f)
            stateImage.sprite = fullHealthSprite;
        else if (ratio > 0.25f)
            stateImage.sprite = mediumHealthSprite;
        else
            stateImage.sprite = lowHealthSprite;
    }
}
