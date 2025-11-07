using PurrNet;
using TMPro;
using UnityEngine;

public class GlobalMoneyUI : NetworkBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private TextMeshProUGUI moneyTextPrefab; 
    private TextMeshProUGUI _moneyTextInstance;

    private GlobalEconomyManager _economyManager;

    private void Start()
    {
        if (!isOwner) return;

        if (moneyTextPrefab != null)
        {
            _moneyTextInstance = Instantiate(moneyTextPrefab);
            _moneyTextInstance.transform.SetParent(GameObject.Find("Canvas").transform, false);
        }

        _economyManager = FindObjectOfType<GlobalEconomyManager>();
        if (_economyManager == null)
            Debug.LogError("GlobalEconomyManager introuvable dans la scène !");
    }

    private void Update()
    {
        if (!isOwner || _moneyTextInstance == null || _economyManager == null) return;

        _moneyTextInstance.text = $"Total Money: {_economyManager.totalMoney}";
    }
}