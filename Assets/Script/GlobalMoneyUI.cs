using PurrNet;
using System.Collections;
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

    private Canvas _mainCanvas;
    private bool _uiInitialized = false;

    [Header("Scenes")]
    [PurrScene, SerializeField] private string _winScene;
    [PurrScene, SerializeField] private string _looseScene;

    [Header("Settings")]
    [SerializeField] private int _moneyTarget = 300;
    private bool _gameEnded = false;

    protected override void OnSpawned()
    {
        base.OnSpawned();

        if (isServer)
        {
            StartCoroutine(SetupUIOnServer());
        }
    }

    private IEnumerator SetupUIOnServer()
    {
        int maxRetries = 50;
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            GameObject canvasGO = GameObject.FindWithTag("MainCanvas");
            if (canvasGO == null)
            {
                canvasGO = GameObject.Find("Canvas");
            }

            if (canvasGO != null && canvasGO.activeInHierarchy)
            {
                _mainCanvas = canvasGO.GetComponent<Canvas>();
                break;
            }

            retryCount++;
            yield return new WaitForSeconds(0.1f);
        }

        if (_mainCanvas == null)
        {
            Debug.LogError("[GlobalMoneyUI] Canvas introuvable!");
            yield break;
        }

        if (moneyTextPrefab != null)
        {
            _moneyTextInstance = Instantiate(moneyTextPrefab, _mainCanvas.transform, false);
        }

        if (timerTextPrefab != null)
        {
            _timerTextInstance = Instantiate(timerTextPrefab, _mainCanvas.transform, false);
        }

        _economyManager = FindObjectOfType<GlobalEconomyManager>();
        if (_economyManager == null)
        {
            Debug.LogError("[GlobalMoneyUI] GlobalEconomyManager introuvable!");
            yield break;
        }

        _uiInitialized = true;
        Debug.Log("[GlobalMoneyUI] UI créée sur le serveur et sera synchronisée aux clients");
    }

    private void Update()
    {
        if (!_uiInitialized || _economyManager == null || _gameEnded)
            return;

        if (_moneyTextInstance != null)
        {
            _moneyTextInstance.text = $"Total Money: {_economyManager.GetTotalMoney()} / {_moneyTarget}";
        }

        if (_timerTextInstance != null)
        {
            float timeRemaining = _economyManager.GetRemainingTime();
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            _timerTextInstance.text = $"Time: {minutes:00}:{seconds:00}";
        }

        if (!isServer)
            return;

        if (_economyManager.GetTotalMoney() >= _moneyTarget)
        {
            _gameEnded = true;
            EndGameRPC(true);
            return;
        }

        // Défaite
        if (_economyManager.GetRemainingTime() <= 0f)
        {
            _gameEnded = true;
            EndGameRPC(false);
            return;
        }
    }

    [ObserversRpc]
    private void EndGameRPC(bool isWin)
    {
        _gameEnded = true;

        if (isWin)
        {
            Debug.Log("Win!");
            SceneManager.LoadSceneAsync(_winScene);
        }
        else
        {
            Debug.Log("Loose!");
            SceneManager.LoadSceneAsync(_looseScene);
        }
    }
}