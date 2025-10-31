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

    private Slider _healthSlider;
    private GameObject _healthBarInstance;
    private HealthBarUI _healthBarUI;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        if (!AllPlayers.Contains(this))
            AllPlayers.Add(this);

        currentHealth = maxHealth;

        if (isOwner && healthBarPrefab != null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                _healthBarInstance = Instantiate(healthBarPrefab, canvas.transform, false);

                _healthSlider = _healthBarInstance.GetComponentInChildren<Slider>();
                _healthBarUI = _healthBarInstance.GetComponent<HealthBarUI>();

                if (_healthSlider != null)
                {
                    _healthSlider.maxValue = maxHealth;
                    _healthSlider.value = currentHealth;
                }

                if (_healthBarUI != null)
                    _healthBarUI.SetHealth(currentHealth, maxHealth);
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

        if (_healthBarUI != null)
            _healthBarUI.SetHealth(currentHealth, maxHealth);
    }

    private void Die()
    {
        if (isOwner)
        {
            SpectatorController spectator = FindAnyObjectByType<SpectatorController>();
            if (spectator != null)
                spectator.ActivateSpectator(this);
        }

        Destroy(gameObject, 0.5f);
    }
}
