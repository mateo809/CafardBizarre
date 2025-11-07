using TMPro;
using UnityEngine;

public class GlobalMoneyUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _moneyText;

    [SerializeField] private GlobalEconomyManager _economyManager;

    private void Update()
    {
        UpdateDisplay(_economyManager.totalMoney);
    }

    private void UpdateDisplay(int newValue)
    {
        _moneyText.text = $"Total Money: {newValue}";
    }
}
