using PurrNet;
using PurrNet.Steam;
using PurrNet.Transports;
using System.Net;
using TMPro;
using UnityEngine;

public class DebugIp : MonoBehaviour
{

    public UDPTransport transport;
    public SteamTransport gggg;
    public TextMeshProUGUI t;


    public void Start()
    {
        if (transport != null)
        {
            t.text = "Ip: " + transport.address;
            Debug.Log("Ip" + transport.address);
        }
        else if (gggg != null)
        {
            t.text = "SteamID: " + gggg.address;
            Debug.Log("SteamID: " + gggg.address);
        }
    }
}
