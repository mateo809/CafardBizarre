using PurrNet;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GlobalMoneyUI : NetworkBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private TextMeshProUGUI moneyTextPrefab;
    [SerializeField] private TextMeshProUGUI timerTextPrefab;
    private TextMeshProUGUI _moneyTextInstance;
    private TextMeshProUGUI _timerTextInstance;
    private GlobalEconomyManager _economyManager;
    private bool _uiInitialized = false;

    [Header("Scenes")]
    [PurrScene, SerializeField] private string _winScene;
    [PurrScene, SerializeField] private string _looseScene;

    [Header("Settings")]
    [SerializeField] private int _moneyTarget = 300;
    private bool _gameEnded = false;

    [Header("Backend")]
    [SerializeField] private BackendCaller backendCaller;

    protected override void OnSpawned()
    {
        base.OnSpawned();
        StartCoroutine(UIManager.Instance.WaitUntilReady(SetupUI));
    }

    private void SetupUI()
    {
        if (moneyTextPrefab != null)
            _moneyTextInstance = UIManager.Instance.InstantiateInCanvas<TextMeshProUGUI>(moneyTextPrefab.gameObject);
        if (timerTextPrefab != null)
            _timerTextInstance = UIManager.Instance.InstantiateInCanvas<TextMeshProUGUI>(timerTextPrefab.gameObject);

        _economyManager = FindObjectOfType<GlobalEconomyManager>();
        if (_economyManager == null)
        {
            Debug.LogError("[GlobalMoneyUI] GlobalEconomyManager introuvable !");
            return;
        }
        _uiInitialized = true;
    }

    private void Update()
    {
        if (!_uiInitialized || _economyManager == null || _gameEnded)
            return;

        if (_moneyTextInstance != null)
            _moneyTextInstance.text = $"Total Money: {_economyManager.GetTotalMoney()} / {_moneyTarget}";

        if (_timerTextInstance != null)
        {
            float t = _economyManager.GetRemainingTime();
            _timerTextInstance.text = $"Time: {Mathf.FloorToInt(t / 60f):00}:{Mathf.FloorToInt(t % 60f):00}";
        }

        if (!isServer) return;

        if (_economyManager.GetTotalMoney() >= _moneyTarget)
        {
            _gameEnded = true;
            EndGameRPC(true);
        }
        else if (_economyManager.GetRemainingTime() <= 0f)
        {
            _gameEnded = true;
            EndGameRPC(false);
        }
    }

    [ObserversRpc]
    private void EndGameRPC(bool isWin)
    {
        _gameEnded = true;

        if (isWin && backendCaller != null)
        {
            StartCoroutine(backendCaller.UnlockRandomCosmetic(
                onSuccess: (cosmetic) =>
                {
                    SceneManager.LoadSceneAsync(_winScene);
                }
            ));
        }
        else
        {
            SceneManager.LoadSceneAsync(isWin ? _winScene : _looseScene);
        }
    }
}