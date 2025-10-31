using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    public static List<PlayerHealth> AllPlayers = new List<PlayerHealth>(); 

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

    private bool _isDead = false;

    void Awake()
    {
        AllPlayers.Add(this);
    }

    void Start()
    {
        currentHealth = maxHealth;

        _colliders = GetComponentsInChildren<Collider>(true);
        _renderers = GetComponentsInChildren<Renderer>(true);
        _controlScripts = GetComponents<MonoBehaviour>();

        if (healthBarPrefab != null)
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

    void OnDestroy()
    {
        AllPlayers.Remove(this);
        if (_healthBarInstance != null)
            Destroy(_healthBarInstance);
    }

    public void TakeDamage(float amount)
    {
        if (_isDead || invincible || amount <= 0f) return;

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
        if (_isDead) return;
        _isDead = true;

        SetPlayerVisible(false);
        SetPlayerControllable(false);

        SpectatorController spectator = FindAnyObjectByType<SpectatorController>();
        if (spectator != null)
        {
            spectator.ActivateSpectator(this);
        }
        else
        {
            Debug.LogWarning("SpectatorController introuvable dans la scène !");
        }
    }

    private void SetPlayerVisible(bool visible)
    {
        foreach (var r in _renderers)
            if (r) r.enabled = visible;
    }

    private void SetPlayerControllable(bool enable)
    {
        foreach (var c in _colliders)
            if (c) c.enabled = enable;

        foreach (var s in _controlScripts)
        {
            if (s == this) continue;
            s.enabled = enable;
        }
    }
}
