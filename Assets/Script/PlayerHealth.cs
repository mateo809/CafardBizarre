using PurrNet;
using System.Collections;
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
    public GameObject Stressbar;

    private Slider _healthSlider;
    private GameObject _healthBarInstance;
    private HealthBarUI _healthBarUI;
    private Canvas _cachedCanvas;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        if (!AllPlayers.Contains(this))
            AllPlayers.Add(this);

        currentHealth = maxHealth;

        // IMPORTANT: Seulement le OWNER affiche son UI
        if (isOwner && healthBarPrefab != null)
        {
            StartCoroutine(SetupHealthUIWithRetry());
        }
    }

    private IEnumerator SetupHealthUIWithRetry()
    {
        int maxRetries = 50; // Max 5 secondes
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            // Cherche d'abord par tag (plus fiable en build)
            GameObject canvasGO = GameObject.FindWithTag("Canvas");
            Canvas canvas = null;

            if (canvasGO != null)
            {
                canvas = canvasGO.GetComponent<Canvas>();
            }

            // Fallback : FindObjectOfType
            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }

            if (canvas != null && canvas.gameObject.activeInHierarchy)
            {
                _cachedCanvas = canvas;
                SetupHealthUI();
                Debug.Log($"[PlayerHealth] Canvas trouvé pour {gameObject.name} (tentative {retryCount + 1})");
                yield break;
            }

            retryCount++;
            yield return new WaitForSeconds(0.1f);
        }

        Debug.LogError($"[PlayerHealth] Canvas introuvable après 5 secondes pour {gameObject.name}!");
    }

    private void SetupHealthUI()
    {
        if (_cachedCanvas == null) return;

        _healthBarInstance = Instantiate(healthBarPrefab, _cachedCanvas.transform, false);
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