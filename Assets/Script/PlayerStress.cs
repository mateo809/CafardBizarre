using UnityEngine;
using UnityEngine.UI;
using PurrNet;
using System.Collections;

public class PlayerStress : NetworkBehaviour
{
    [Header("Stress")]
    [Range(0, 100)]
    public float currentStress = 0f;

    [Header("UI")]
    public GameObject stressBarPrefab;
    private GameObject _stressBarInstance;
    private Canvas _cachedCanvas;

    [Header("Stress Logic")]
    public float increaseRate = 20f;
    public float decreaseRate = 10f;
    private bool _isDetected = false;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        // IMPORTANT: Seulement le OWNER (client local) affiche son UI
        if (isOwner && stressBarPrefab != null)
        {
            StartCoroutine(SetupStressUIWithRetry());
        }
    }

    private IEnumerator SetupStressUIWithRetry()
    {
        int maxRetries = 50; // Max 5 secondes
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            // Cherche d'abord par tag
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
                SetupStressUI();
                Debug.Log($"[PlayerStress] Canvas trouvé pour {gameObject.name} (tentative {retryCount + 1})");
                yield break;
            }

            retryCount++;
            yield return new WaitForSeconds(0.1f);
        }

        Debug.LogError($"[PlayerStress] Canvas introuvable après 5 secondes pour {gameObject.name}!");
    }

    private void SetupStressUI()
    {
        if (_cachedCanvas == null) return;

        _stressBarInstance = Instantiate(stressBarPrefab, _cachedCanvas.transform, false);
        StressGauge gauge = _stressBarInstance.GetComponentInChildren<StressGauge>();

        if (gauge != null)
            gauge.SetPlayerStress(this);
    }

    private void OnDestroy()
    {
        if (_stressBarInstance != null)
            Destroy(_stressBarInstance);
    }

    void Update()
    {
        if (!isOwner) return;

        if (_isDetected)
        {
            currentStress = Mathf.Clamp(currentStress + increaseRate * Time.deltaTime, 0f, 100f);
        }
        else
        {
            currentStress = Mathf.Clamp(currentStress - decreaseRate * Time.deltaTime, 0f, 100f);
        }
    }

    public void SetDetected(bool detected)
    {
        _isDetected = detected;
    }
}