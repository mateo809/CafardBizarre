using System.Collections;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.Networking;

public class TestBackend : MonoBehaviour
{
    private string token = "TOKEN_super_secret_et_COOL";

    void Start()
    {
        StartCoroutine(GetMe());
    }

    IEnumerator Ping()
    {
        UnityWebRequest req =
            UnityWebRequest.Get("http://localhost:3000/ping");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Backend response: " + req.downloadHandler.text);
        }
        else
        {
            Debug.LogError("Error: " + req.error);
        }
    }

    IEnumerator GetMe()
    {
        UnityWebRequest req =
            UnityWebRequest.Get("http://localhost:3000/user/me");

        req.SetRequestHeader(
            "Authorization",
            "Bearer " + token
        );

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log(req.downloadHandler.text);
        }
        else
        {
            Debug.LogError(req.error);
        }
    }
}
