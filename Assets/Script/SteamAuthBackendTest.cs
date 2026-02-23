using Steamworks;
using System;
using System.Collections;

using UnityEngine;
using UnityEngine.Networking;
using System.Text;

public class SteamAuthBackendTest
 : MonoBehaviour
{
    private Callback<GetTicketForWebApiResponse_t> webApiTicketCallback;
    private HAuthTicket authTicket;

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
            Debug.Log("Backend response: " + req.downloadHandler.text);
        }
        else
        {
            Debug.LogError(req.error);
        }
    }

    private void OnWebApiTicket(GetTicketForWebApiResponse_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("Failed to get Web API ticket: " + callback.m_eResult);
            return;
        }

        byte[] ticket = callback.m_rgubTicket;
        uint ticketSize = (uint) callback.m_cubTicket;

        string ticketHex = BitConverter
            .ToString(ticket, 0, (int)ticketSize)
            .Replace("-", "");
        StartCoroutine(SendTicketToBackend(ticketHex));

        Debug.Log("Steam Web API Ticket (HEX): " + ticketHex);

        
    }
}
