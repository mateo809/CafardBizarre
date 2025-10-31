using UnityEngine;
using UnityEngine.UI;
using PurrNet;

public class PlayerStress : NetworkBehaviour
{
    [Header("Stress")]
    [Range(0, 100)]
    public float currentStress = 0f;

    [Header("UI")]
    public GameObject stressBarPrefab;
    private GameObject _stressBarInstance;

    [Header("Stress Logic")]
    public float increaseRate = 20f; 
    public float decreaseRate = 10f;

    private bool _isDetected = false;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        if (isOwner && stressBarPrefab != null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                _stressBarInstance = Instantiate(stressBarPrefab, canvas.transform, false);

                StressGauge gauge = _stressBarInstance.GetComponentInChildren<StressGauge>();
                if (gauge != null)
                {
                    gauge.SetPlayerStress(this); 
                }
            }
        }
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

