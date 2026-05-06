// BackendCaller.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class BackendCaller : MonoBehaviour
{
    [SerializeField] private AuthData authData;
    [SerializeField] private string baseUrl = "http://localhost:3000";

    public IEnumerator GetMe()
    {
        using var req = UnityWebRequest.Get(baseUrl + "/user/me");
        if (authData.IsAuthenticated)
            req.SetRequestHeader("Authorization", "Bearer " + authData.Token);
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log("User data: " + req.downloadHandler.text);
        else
            Debug.LogError("GetMe error: " + req.error);
    }

    public IEnumerator EquipCosmetic(int cosmeticId, string cosmeticName)
    {
        string json = $"{{\"skinId\": {cosmeticId}}}";
        using var req = new UnityWebRequest(baseUrl + "/cosmetics/equip-skin", "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        if (authData.IsAuthenticated)
            req.SetRequestHeader("Authorization", "Bearer " + authData.Token);
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log($"Cosmetic équipé : {cosmeticName} (id: {cosmeticId})");
        else
            Debug.LogError("EquipCosmetic error: " + req.error);
    }

    public IEnumerator GetEquippedCosmetic(System.Action<int> onSuccess = null)
    {
        using var req = UnityWebRequest.Get(baseUrl + "/cosmetics/equipped");
        if (authData.IsAuthenticated)
            req.SetRequestHeader("Authorization", "Bearer " + authData.Token);
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
        {
            var response = JsonUtility.FromJson<EquippedCosmeticResponse>(req.downloadHandler.text);
            Debug.Log($"Cosmetic équipé : {response.equiped_cosmetic}");
            onSuccess?.Invoke(response.equiped_cosmetic);
        }
        else
            Debug.LogError("GetEquippedCosmetic error: " + req.error);
    }

    public IEnumerator UnlockCosmetic(int cosmeticId, System.Action onSuccess = null)
    {
        string json = $"{{\"cosmeticId\": {cosmeticId}}}";
        using var req = new UnityWebRequest(baseUrl + "/cosmetics/unlock", "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        if (authData.IsAuthenticated)
            req.SetRequestHeader("Authorization", "Bearer " + authData.Token);
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"Cosmetic débloqué : {cosmeticId}");
            onSuccess?.Invoke();
        }
        else
            Debug.LogError("UnlockCosmetic error: " + req.error);
    }

    public IEnumerator GetUnlockedCosmetics(System.Action<List<CosmeticData>> onSuccess = null)
    {
        using var req = UnityWebRequest.Get(baseUrl + "/cosmetics/unlocked");
        if (authData.IsAuthenticated)
            req.SetRequestHeader("Authorization", "Bearer " + authData.Token);
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("RAW unlocked: " + req.downloadHandler.text); 
            string json = $"{{\"items\":{req.downloadHandler.text}}}";
            var response = JsonUtility.FromJson<CosmeticListResponse>(json);
            Debug.Log($"{response.items.Count} cosmetics débloqués");
            onSuccess?.Invoke(response.items);
        }
        else
            Debug.LogError("GetUnlockedCosmetics error: " + req.error);
    }

    public IEnumerator UnlockRandomCosmetic(System.Action<CosmeticData> onSuccess = null)
    {
        using var req = new UnityWebRequest(baseUrl + "/cosmetics/unlock-random", "POST");
        req.uploadHandler = new UploadHandlerRaw(new byte[0]);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        if (authData.IsAuthenticated)
            req.SetRequestHeader("Authorization", "Bearer " + authData.Token);

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var response = JsonUtility.FromJson<UnlockRandomResponse>(req.downloadHandler.text);
            Debug.Log($"Nouveau cosmetic débloqué : {response.unlockedCosmetic.name}");
            onSuccess?.Invoke(response.unlockedCosmetic);
        }
        else
            Debug.LogError("UnlockRandomCosmetic error: " + req.error);
    }

    public IEnumerator UpdateHighscore(int timer, int money)
    {
        string json = $"{{\"highscore_timer\": {timer}, \"highscore_money\": {money}}}";
        using var req = new UnityWebRequest(baseUrl + "/user/update", "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        if (authData.IsAuthenticated)
            req.SetRequestHeader("Authorization", "Bearer " + authData.Token);

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log($"Highscore mis à jour : timer={timer}, money={money}");
        else
            Debug.LogError("UpdateHighscore error: " + req.error);
    }


}

[System.Serializable]
public class UnlockRandomResponse
{
    public CosmeticData unlockedCosmetic;
}

[System.Serializable]
public class EquippedCosmeticResponse
{
    public int equiped_cosmetic;
}

[System.Serializable]
public class CosmeticData
{
    public int id;
    public string name;
}

[System.Serializable]
public class CosmeticListResponse
{
    public List<CosmeticData> items;
}