// BackendCaller.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class BackendCaller : MonoBehaviour
{
    [SerializeField] private AuthData authData;
    [SerializeField] private string baseUrl = "http://localhost:3000";

    private void Start()
    {
    }
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
}