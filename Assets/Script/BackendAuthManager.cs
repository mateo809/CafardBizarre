using Steamworks;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

public class BackendAuthManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public static BackendAuthManager Instance;

    private Callback<GetTicketForWebApiResponse_t> webApiTicketCallback;
    private HAuthTicket authTicket;

    public string Token;

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

    void Start()
    {
        if (!SteamAPI.IsSteamRunning())
        {
            Debug.LogError("Steam not running");
            return;
        }

        webApiTicketCallback =
            Callback<GetTicketForWebApiResponse_t>.Create(OnWebApiTicket);

        Debug.Log("Requesting Steam Web API ticket...");
        authTicket = SteamUser.GetAuthTicketForWebApi("unity");
    }

    private void OnWebApiTicket(GetTicketForWebApiResponse_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("Failed to get ticket: " + callback.m_eResult);
            return;
        }

        byte[] ticket = callback.m_rgubTicket;
        uint ticketSize = (uint)callback.m_cubTicket;

        string ticketHex = BitConverter
            .ToString(ticket, 0, (int)ticketSize)
            .Replace("-", "");

        Debug.Log("Ticket HEX: " + ticketHex);

        StartCoroutine(SendTicketToBackend(ticketHex));
    }

    IEnumerator SendTicketToBackend(string ticketHex)
    {
        string json = "{\"ticket\":\"" + ticketHex + "\"}";

        UnityWebRequest req = new UnityWebRequest(
            "http://localhost:3000/auth/steam",
            "POST"
        );

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Login success: " + req.downloadHandler.text);

            // 🔥 EXTRACTION DU TOKEN (simple)
            var response = JsonUtility.FromJson<AuthResponse>(req.downloadHandler.text);
            Token = response.token;

            Debug.Log("Token saved: " + Token);
        }
        else
        {
            Debug.LogError("Auth error: " + req.error);
        }
    }

    [Serializable]
    public class AuthResponse
    {
        public string token;
    }
}
