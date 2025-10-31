using PurrNet;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : NetworkBehaviour
{
    public static readonly List<PlayerHealth> AllPlayers = new List<PlayerHealth>();

    [Header("Stats")]
    public float currentHealth;
    public float maxHealth = 100f;
    public bool invincible = false;

    [Header("UI")]
    public GameObject healthBarPrefab;

    private GameObject _healthBarInstance;
    private Slider _healthSlider;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        // Ajoute tous les joueurs à la liste globale (pour le spectateur)
        if (!AllPlayers.Contains(this))
            AllPlayers.Add(this);

        currentHealth = maxHealth;

        // Initialise la barre de vie uniquement côté owner
        if (isOwner && healthBarPrefab != null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                _healthBarInstance = Instantiate(healthBarPrefab, canvas.transform, false);
                _healthSlider = _healthBarInstance.GetComponent<Slider>();

                if (_healthSlider != null)
                {
                    _healthSlider.maxValue = maxHealth;
                    _healthSlider.value = currentHealth;
                }
            }
        }
    }

    private void OnDestroy()
    {
        AllPlayers.Remove(this);

        if (_healthBarInstance != null)
            Destroy(_healthBarInstance);
    }

    public void TakeDamage(float amount)
    {
        if (!isOwner || invincible || amount <= 0f) return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        UpdateHealthUI();

        if (currentHealth <= 0f)
            Die();
    }

    private void UpdateHealthUI()
    {
        if (_healthSlider != null)
            _healthSlider.value = currentHealth;
    }

    private void Die()
    {
        // Active le mode spectateur pour le joueur local
        if (isOwner)
        {
            SpectatorController spectator = FindAnyObjectByType<SpectatorController>();
            if (spectator != null)
                spectator.ActivateSpectator(this);
        }

        // Laisse un court délai avant destruction pour éviter le "0 joueur vivant"
        Destroy(gameObject, 0.5f);
    }
}
