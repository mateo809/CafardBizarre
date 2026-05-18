
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

    public IEnumerator GetParties(System.Action<List<PartyData>> onSuccess = null)
    {
        using var req = UnityWebRequest.Get(baseUrl + "/party/get-parties");
        if (authData.IsAuthenticated)
            req.SetRequestHeader("Authorization", "Bearer " + authData.Token);
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
        {
            string json = $"{{\"items\":{req.downloadHandler.text}}}";
            var response = JsonUtility.FromJson<PartyListResponse>(json);
            Debug.Log($"{response.items.Count} parties récupérées");
            onSuccess?.Invoke(response.items);
        }
        else
            Debug.LogError("GetParties error: " + req.error);
    }

    public IEnumerator SaveParty(int idParty, int actualMoney, int actualDay,
                                 string extraData = "{}",
                                 System.Action<PartyData> onSuccess = null)
    {
        string json = $"{{" +
                      $"\"id_party\": {idParty}," +
                      $"\"actual_money\": {actualMoney}," +
                      $"\"actual_day\": {actualDay}," +
                      $"\"data\": {extraData}" +
                      $"}}";

        using var req = new UnityWebRequest(baseUrl + "/party/save", "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        if (authData.IsAuthenticated)
            req.SetRequestHeader("Authorization", "Bearer " + authData.Token);
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
        {
            var saved = JsonUtility.FromJson<PartyData>(req.downloadHandler.text);
            Debug.Log($"Partie sauvegardée : id_party={idParty}, jour={actualDay}, argent={actualMoney}");
            onSuccess?.Invoke(saved);
        }
        else
            Debug.LogError("SaveParty error: " + req.error);
    }

    public IEnumerator DeleteParty(int partyId, System.Action onSuccess = null)
    {
        using var req = new UnityWebRequest(baseUrl + "/party/delete/" + partyId, "DELETE");
        req.downloadHandler = new DownloadHandlerBuffer();
        if (authData.IsAuthenticated)
            req.SetRequestHeader("Authorization", "Bearer " + authData.Token);
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"Partie {partyId} supprimée");
            onSuccess?.Invoke();
        }
        else
            Debug.LogError("DeleteParty error: " + req.error);
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

[System.Serializable]
public class PartyData
{
    public int id;           
    public int id_party;     
    public int user_id;
    public int actual_money;
    public int actual_day;
    public string data;         
    public string created_at;
}

[System.Serializable]
public class PartyListResponse
{
    public List<PartyData> items;
}