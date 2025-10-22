using PurrNet;
using PurrNet.Steam;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Steamworks; // Steamworks.NET

namespace SteamExample
{
    public sealed class ConnexionManager : NetworkIdentity
    {
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private TMP_Text hostTextField;
        [SerializeField] private TMP_InputField clientInputField;

        private bool steamInitialized;

        private void Awake()
        {
            InstanceHandler.RegisterInstance(this);

            if(InstanceHandler.GetInstance<ConnexionManager>() != this)
            {
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(this);

            InitializeSteam();
        }

        private void OnEnable()
        {
            hostButton?.onClick.AddListener(HandleHostClicked);
            clientButton?.onClick.AddListener(HandleClientClicked);
        }

        private void OnDisable()
        {
            hostButton?.onClick.RemoveListener(HandleHostClicked);
            clientButton?.onClick.RemoveListener(HandleClientClicked);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            InstanceHandler.UnregisterInstance<ConnexionManager>();

            if (steamInitialized)
            {
#if !UNITY_EDITOR
        SteamAPI.Shutdown();
#endif
                steamInitialized = false;
            }
        }

        private void Update()
        {
            if (steamInitialized)
            {
                SteamAPI.RunCallbacks();
            }
        }

        private void InitializeSteam()
        {
            try
            {
                // Optionnel : forcer le redémarrage si l'AppID n'est pas le bon :
                // if (SteamAPI.RestartAppIfNecessary((AppId_t)480)) { Application.Quit(); return; }

                if (!SteamAPI.Init())
                {
                    Debug.LogError("SteamAPI.Init() a échoué.");
                    steamInitialized = false;
                    return;
                }

                steamInitialized = true;

                // Récupère le SteamID local
                CSteamID localId = SteamUser.GetSteamID();
                ulong steam64 = localId.m_SteamID; // ou localId.ToUInt64() si disponible

                Debug.Log($"✅ Steam initialisé : {SteamFriends.GetPersonaName()} (Steam64: {steam64})");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Exception lors de l'initialisation Steam : {e}");
                steamInitialized = false;
            }
        }

        // Host
        public void StartHost()
        {
            var steamTransport = NetworkManager.main.transport as SteamTransport;
            if (steamTransport == null)
            {
                Debug.LogError("SteamTransport manquant sur le NetworkManager", this);
                return;
            }

            if (!steamInitialized)
            {
                Debug.LogError("Steam non initialisé !");
                return;
            }

            CSteamID localId = SteamUser.GetSteamID();
            ulong steam64 = localId.m_SteamID;

            steamTransport.peerToPeer = true;
            steamTransport.dedicatedServer = false;
            steamTransport.address = steam64.ToString();

            NetworkManager.main.StartHost();

            Debug.Log($"🟢 Serveur Steam lancé pour SteamID64: {steam64}");
        }

        // Client
        public void StartClient(string steamIdString)
        {
            var steamTransport = NetworkManager.main.transport as SteamTransport;
            if (steamTransport == null)
            {
                Debug.LogError("SteamTransport manquant sur le NetworkManager", this);
                return;
            }

            if (string.IsNullOrEmpty(steamIdString))
            {
                Debug.LogError("Adresse SteamID vide");
                return;
            }

            if (!ulong.TryParse(steamIdString, out var hostId))
            {
                Debug.LogError("SteamID invalide.");
                return;
            }

            steamTransport.peerToPeer = true;
            steamTransport.dedicatedServer = false;
            steamTransport.address = hostId.ToString();

            NetworkManager.main.StartClient();

            Debug.Log($"🟡 Connexion au serveur SteamID: {hostId}");
        }

        // UI handlers
        private void HandleHostClicked()
        {
            if (hostButton == null || hostTextField == null)
            {
                Debug.LogError("Bouton ou texte Host manquant.");
                return;
            }

            StartHost();

            if (NetworkManager.main.isOffline)
            {
                hostTextField.text = "Serveur Offline ❌";
                hostButton.image.color = Color.red;
                return;
            }

            CSteamID localId = SteamUser.GetSteamID();
            hostTextField.text = localId.m_SteamID.ToString();
            hostButton.image.color = Color.green;

            hostButton.onClick.RemoveListener(HandleHostClicked);
            clientButton.onClick.RemoveListener(HandleClientClicked);
        }

        private void HandleClientClicked()
        {
            if (string.IsNullOrEmpty(clientInputField.text))
            {
                clientInputField.text = "Entrez l'ID Steam de l'hôte";
                clientButton.image.color = Color.red;
                return;
            }

            StartClient(clientInputField.text);

            clientInputField.text = $"Connexion à {clientInputField.text}";
            clientButton.image.color = Color.green;

            clientButton.onClick.RemoveListener(HandleClientClicked);
            hostButton.onClick.RemoveListener(HandleHostClicked);
        }
    }
}
