using PurrNet;
using UnityEngine;

public class GlobalEconomyManager : NetworkBehaviour
{

    public SyncVar<int> totalMoney = new SyncVar<int>(0);

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
