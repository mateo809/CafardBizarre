using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class BackendCaller : MonoBehaviour
{

    public static BackendCaller Instance;
    private string baseUrl = "http://localhost:3000";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public IEnumerator GetMe()
    {
        UnityWebRequest req =
            UnityWebRequest.Get(baseUrl + "/user/me");

        if (!string.IsNullOrEmpty(BackendAuthManager.Instance.Token))
        {
            req.SetRequestHeader(
                "Authorization",
                "Bearer " + BackendAuthManager.Instance.Token
            );
        }

        yield return req.SendWebRequest();
        
        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("User data: " + req.downloadHandler.text);
        }
        else
        {
            Debug.LogError("GetMe error: " + req.error);
        }
    }
}