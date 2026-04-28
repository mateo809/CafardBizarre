using UnityEngine;
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
            StartCoroutine(UIManager.Instance.WaitUntilReady(SetupStressUI));
    }

    private void SetupStressUI()
    {
        _stressBarInstance = UIManager.Instance.InstantiateInCanvas(stressBarPrefab);
        if (_stressBarInstance == null) return;

        StressGauge gauge = _stressBarInstance.GetComponentInChildren<StressGauge>();
        if (gauge != null)
            gauge.SetPlayerStress(this);
    }

    private void OnDestroy()
    {
        if (_stressBarInstance != null)
            Destroy(_stressBarInstance);
    }

    private void Update()
    {
        if (!isOwner) return;

        float delta = (_isDetected ? increaseRate : -decreaseRate) * Time.deltaTime;
        currentStress = Mathf.Clamp(currentStress + delta, 0f, 100f);
    }

    public void SetDetected(bool detected) => _isDetected = detected;
}