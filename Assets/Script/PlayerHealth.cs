using PurrNet;
using System.Collections;
using TMPro;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Stats")]
    public float currentHealth;
    public float maxHealth = 100f;
    public bool invincible = false;

    [Header("Respawn")]
    public float respawnCooldown = 5f;
    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;

    [Header("UI")]
    public GameObject respawnCanvasPrefab;
    private GameObject _respawnCanvasInstance;
    private TMP_Text _respawnText;

    private Collider[] _colliders;
    private Renderer[] _renderers;
    private MonoBehaviour[] _controlScripts;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        if (!isOwner)
        {
            enabled = false;
            return;
        }

        enabled = true;
        currentHealth = maxHealth;

        _spawnPosition = transform.position;
        _spawnRotation = transform.rotation;

        _colliders = GetComponentsInChildren<Collider>(true);
        _renderers = GetComponentsInChildren<Renderer>(true);
        _controlScripts = GetComponents<MonoBehaviour>();

        if (respawnCanvasPrefab != null && _respawnCanvasInstance == null)
        {
            _respawnCanvasInstance = Instantiate(respawnCanvasPrefab);
            _respawnCanvasInstance.SetActive(false);

            if (_respawnCanvasInstance.transform.childCount >= 3)
            {
                var child = _respawnCanvasInstance.transform.GetChild(2);
                _respawnText = child.GetComponent<TMP_Text>();
            }
            else
            {
                Debug.LogWarning("pas assez d’enfants");
            }
        }
    }

    public void TakeDamage(float amount)
    {
        if (!isOwner || invincible || amount <= 0f) return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        if (currentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (!isOwner || amount <= 0f) return;

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    private void Die()
    {
        if (_respawnCanvasInstance)
            _respawnCanvasInstance.SetActive(true);

        SetPlayerVisible(false);
        SetPlayerControllable(false);

        transform.position = new Vector3(0, -100f, 0);

        StartCoroutine(_RespawnCoroutine());
    }

    private IEnumerator _RespawnCoroutine()
    {
        float timer = respawnCooldown;

        while (timer > 0f)
        {
            if (_respawnText) _respawnText.text = Mathf.Ceil(timer).ToString();
            yield return new WaitForSeconds(1f);
            timer -= 1f;
        }

        currentHealth = maxHealth;

        transform.position = _spawnPosition;
        transform.rotation = _spawnRotation;

        SetPlayerVisible(true);
        SetPlayerControllable(true);

        if (_respawnCanvasInstance)
            _respawnCanvasInstance.SetActive(false);
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
