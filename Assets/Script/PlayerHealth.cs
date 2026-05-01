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

    [Header("Regeneration")]
    [SerializeField] private float regenDelay = 5f;
    [SerializeField] private float regenPerSecond = 10f;

    [Header("UI")]
    public GameObject healthBarPrefab;

    private Slider _healthSlider;
    private GameObject _healthBarInstance;
    private HealthBarUI _healthBarUI;

    private float _timeSinceLastDamage;
    private bool _isRegenerating;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        if (!AllPlayers.Contains(this))
            AllPlayers.Add(this);

        currentHealth = maxHealth;
        _timeSinceLastDamage = 0f;
        _isRegenerating = false;

        if (isOwner && healthBarPrefab != null)
            StartCoroutine(UIManager.Instance.WaitUntilReady(SetupHealthUI));
    }

    private void Update()
    {
        if (!isOwner) return;

        if (currentHealth < maxHealth)
        {
            _timeSinceLastDamage += Time.deltaTime;

            if (_timeSinceLastDamage >= regenDelay)
            {
                _isRegenerating = true;
                currentHealth = Mathf.MoveTowards(
                    currentHealth,
                    maxHealth,
                    regenPerSecond * Time.deltaTime
                );
                UpdateHealthUI();

                if (currentHealth >= maxHealth)
                {
                    currentHealth = maxHealth;
                    _isRegenerating = false;
                }
            }
        }
        else
        {
            _timeSinceLastDamage = 0f;
            _isRegenerating = false;
        }
    }

    private void SetupHealthUI()
    {
        _healthBarInstance = UIManager.Instance.InstantiateInCanvas(healthBarPrefab);
        if (_healthBarInstance == null) return;

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

    private void OnDestroy()
    {
        AllPlayers.Remove(this);
        if (_healthBarInstance != null)
            Destroy(_healthBarInstance);
    }

    public void TakeDamage(float amount)
    {
        if (!isOwner || invincible || amount <= 0f) return;

        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
        UpdateHealthUI();

        _timeSinceLastDamage = 0f;
        _isRegenerating = false;

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
            AudioController.Instance.PlaySound(
                AudioType.Die,
                AudioSourceType.Player,
                transform.position
            );

            SpectatorController spectator = FindAnyObjectByType<SpectatorController>();
            if (spectator != null)
                spectator.ActivateSpectator(this);
        }

        Destroy(gameObject, 0.5f);
    }
}