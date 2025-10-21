using UnityEngine;
using Unity.Netcode;

public class NetworkStartUI : MonoBehaviour
{
    void OnGUI()
    {
        float buttonWidth = 200f;
        float buttonHeight = 40f;
        float spacing = 15f; 

        float x = 10f;
        float y = 10f;

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "Start Host"))
            {
                NetworkManager.Singleton.StartHost();
            }

            y += buttonHeight + spacing;

            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "Start Client"))
            {
                NetworkManager.Singleton.StartClient();
            }

            y += buttonHeight + spacing;

            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "Start Server"))
            {
                NetworkManager.Singleton.StartServer();
            }
        }
        else
        {
            GUI.Label(new Rect(x, y, buttonWidth + 100f, buttonHeight), "Network is running...");
        }
    }
}
