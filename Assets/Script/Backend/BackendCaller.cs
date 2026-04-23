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
        Debug.LogWarning("teeeeeeeeeeeeeeeeest");
    }
    public IEnumerator GetMe()
    {
        Debug.LogWarning("teeeeeeeeeeeeeeeeest");

        using var req = UnityWebRequest.Get(baseUrl + "/user/me");
        Debug.LogWarning("teeeeeeeeeeeeeeeeest");
        if (authData.IsAuthenticated)
            req.SetRequestHeader("Authorization", "Bearer " + authData.Token);

        yield return req.SendWebRequest();
        Debug.LogWarning("teeeeeeeeeeeeeeeeest");
        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log("User data: " + req.downloadHandler.text);
        else
            Debug.LogError("GetMe error: " + req.error);
    }
}