using TMPro;
using UnityEngine;

public class GlobalMoneyUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _moneyText;

    private void Update()
    {
        UpdateDisplay(GlobalEconomyManager.Instance.GetTotalMoney());
    }

    private void UpdateDisplay(int newValue)
    {
        _moneyText.text = $"Total Money: {newValue}";
    }
}
