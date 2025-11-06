using PurrNet;
using UnityEngine;

public class GlobalEconomyManager : NetworkBehaviour
{
    public static GlobalEconomyManager Instance { get; private set; }

    public SyncVar<int> totalMoney = new SyncVar<int>(0);

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;
    }

    protected override void OnSpawned()
    {
        base.OnSpawned();
    }

    public void AddMoneyServerRpc(int amount)
    {
        totalMoney.value += amount;
    }

    public int GetTotalMoney()
    {
        return totalMoney.value;
    }
}
