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

        AllPlayers.Add(this); // ajouter à la liste globale

        if (!isOwner)
        {
            enabled = false;
            return;
        }

        enabled = true;
        currentHealth = maxHealth;

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
        AllPlayers.Remove(this); // retirer de la liste globale
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
        RPC_SetPlayerDead();

        if (isOwner)
        {
            SpectatorController spectator = FindAnyObjectByType<SpectatorController>();
            if (spectator != null)
            {
                Debug.Log("trouver");
                spectator.ActivateSpectator(this);
            }
            else
            {
                Debug.LogWarning("SpectatorController introuvable dans la scène !");
            }
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

    [ServerRpc]
    private void RPC_SetPlayerDead()
    {
        SetPlayerVisible(false);
        SetPlayerControllable(false);

    }

}
