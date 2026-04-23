// BackendAuthManager.cs
using System;
using System.Collections;
using System.Text;
using Steamworks;
using UnityEngine;
using UnityEngine.Networking;

public class BackendAuthManager : MonoBehaviour
{
    [SerializeField] private AuthData authData;
    [SerializeField] private string baseUrl = "http://localhost:3000";

    private Callback<GetTicketForWebApiResponse_t> _webApiTicketCallback;

    void Awake()
    {
        if (authData.IsAuthenticated)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (!SteamAPI.IsSteamRunning())
        {
            Debug.LogError("Steam not running");
            return;
        }

        _webApiTicketCallback =
            Callback<GetTicketForWebApiResponse_t>.Create(OnWebApiTicket);

        SteamUser.GetAuthTicketForWebApi("unity");
    }
    private void OnWebApiTicket(GetTicketForWebApiResponse_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("Failed to get ticket: " + callback.m_eResult);
            return;
        }

        string ticketHex = BitConverter
            .ToString(callback.m_rgubTicket, 0, callback.m_cubTicket)
            .Replace("-", "");

        StartCoroutine(SendTicketToBackend(ticketHex));
    }

    private IEnumerator SendTicketToBackend(string ticketHex)
    {
        string json = $"{{\"ticket\":\"{ticketHex}\"}}";
        byte[] body = Encoding.UTF8.GetBytes(json);

        using var req = new UnityWebRequest(baseUrl + "/auth/steam", "POST");
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var response = JsonUtility.FromJson<AuthResponse>(req.downloadHandler.text);
            authData.Token = response.token;
            Debug.Log("Auth OK, token saved.");
        }
        else
        {
            Debug.LogError("Auth error: " + req.error);
        }
    }

    [Serializable]
    private class AuthResponse { public string token; }
}