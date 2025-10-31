using PurrNet;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : NetworkBehaviour
{
    public static List<PlayerHealth> AllPlayers = new List<PlayerHealth>(); // liste globale

    [Header("Stats")]
    public float currentHealth;
    public float maxHealth = 100f;
    public bool invincible = false;

    [Header("UI")]
    public GameObject healthBarPrefab;

    private GameObject _healthBarInstance;
    private Slider _healthSlider;

    private Collider[] _colliders;
    private Renderer[] _renderers;
    private MonoBehaviour[] _controlScripts;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        AllPlayers.Add(this);

        if (!isOwner)
        {
            enabled = false;
            return;
        }

        currentHealth = maxHealth;
        enabled = true;

        _colliders = GetComponentsInChildren<Collider>(true);
        _renderers = GetComponentsInChildren<Renderer>(true);
        _controlScripts = GetComponents<MonoBehaviour>();

        // Health Bar UI
        if (healthBarPrefab != null && _healthBarInstance == null)
        {
            Canvas canvas = GameObject.FindObjectOfType<Canvas>();
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

        // Active le spectateur
        SpectatorController spectator = FindAnyObjectByType<SpectatorController>();
        if (spectator != null)
            spectator.ActivateSpectator(this);

        // Détruit après un court délai pour que le spectateur puisse récupérer les autres
        Destroy(gameObject, 0.5f);
    }

}
