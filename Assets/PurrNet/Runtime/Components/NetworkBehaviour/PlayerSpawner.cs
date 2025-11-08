using System.Collections;
using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Modules;
using UnityEngine;

namespace PurrNet
{
    public class PlayerSpawner : PurrMonoBehaviour
    {
        [SerializeField, HideInInspector] private NetworkIdentity playerPrefab;
        [SerializeField] private GameObject _playerPrefab;
        [Tooltip("Even if rules are to not despawn on disconnect, this will ignore that and always spawn a player.")]
        [SerializeField] private bool _ignoreNetworkRules;

        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
        private int _currentSpawnPoint;

        private void Awake()
        {
            CleanupSpawnPoints();
        }

        private void CleanupSpawnPoints()
        {
            bool hadNullEntry = false;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                if (!spawnPoints[i])
                {
                    hadNullEntry = true;
                    spawnPoints.RemoveAt(i);
                    i--;
                }
            }

            if (hadNullEntry)
                PurrLogger.LogWarning("Some spawn points were invalid and have been cleaned up.", this);
        }

        private void OnValidate()
        {
            if (playerPrefab)
            {
                _playerPrefab = playerPrefab.gameObject;
                playerPrefab = null;
            }
        }

        public override void Subscribe(NetworkManager manager, bool asServer)
        {
            if (asServer && manager.TryGetModule(out ScenePlayersModule scenePlayersModule, true))
            {
                scenePlayersModule.onPlayerLoadedScene += OnPlayerLoadedScene;
            }
        }

        public override void Unsubscribe(NetworkManager manager, bool asServer)
        {
            if (asServer && manager.TryGetModule(out ScenePlayersModule scenePlayersModule, true))
                scenePlayersModule.onPlayerLoadedScene -= OnPlayerLoadedScene;
        }

        private void OnDestroy()
        {
            if (NetworkManager.main &&
                NetworkManager.main.TryGetModule(out ScenePlayersModule scenePlayersModule, true))
                scenePlayersModule.onPlayerLoadedScene -= OnPlayerLoadedScene;
        }

        private void OnPlayerLoadedScene(PlayerID player, SceneID scene, bool asServer)
        {
            var main = NetworkManager.main;
            if (!main || !main.TryGetModule(out ScenesModule scenes, true))
                return;

            var unityScene = gameObject.scene;
            if (!scenes.TryGetSceneID(unityScene, out var sceneID))
                return;

            if (sceneID != scene || !asServer)
                return;

            StartCoroutine(WaitAndSpawnPlayer(player, unityScene));
        }

        private IEnumerator WaitAndSpawnPlayer(PlayerID player, UnityEngine.SceneManagement.Scene unityScene)
        {
            float waitTime = 0f;
            IntroManager introManager = Object.FindObjectOfType<IntroManager>();

            // Attendre la fin de l'intro (max 60 secondes)
            while (introManager != null && !introManager.IsIntroPlaying() && waitTime < 60f)
            {
                waitTime += Time.deltaTime;
                yield return null;
            }

            // Si IntroManager est présent et que l’intro est encore en cours, attendre qu’elle se termine
            while (introManager != null && introManager.IsIntroPlaying() && waitTime < 60f)
            {
                waitTime += Time.deltaTime;
                yield return null;
            }

            // Spawn du joueur
            SpawnPlayer(player, unityScene);
        }

        private void SpawnPlayer(PlayerID player, UnityEngine.SceneManagement.Scene unityScene)
        {
            var main = NetworkManager.main;
            if (!main) return;

            bool isDestroyOnDisconnectEnabled = main.networkRules.ShouldDespawnOnOwnerDisconnect();

            if (!_ignoreNetworkRules &&
                !isDestroyOnDisconnectEnabled &&
                main.TryGetModule(out GlobalOwnershipModule ownership, true) &&
                ownership.PlayerOwnsSomething(player))
                return;

            CleanupSpawnPoints();

            GameObject newPlayer;

            if (spawnPoints.Count > 0)
            {
                var spawnPoint = spawnPoints[_currentSpawnPoint];
                _currentSpawnPoint = (_currentSpawnPoint + 1) % spawnPoints.Count;
                newPlayer = UnityProxy.Instantiate(_playerPrefab, spawnPoint.position, spawnPoint.rotation, unityScene);
            }
            else
            {
                _playerPrefab.transform.GetPositionAndRotation(out var position, out var rotation);
                newPlayer = UnityProxy.Instantiate(_playerPrefab, position, rotation, unityScene);
            }

            // Donne la possession réseau au joueur
            if (newPlayer.TryGetComponent(out NetworkIdentity identity))
                identity.GiveOwnership(player);

            // S'assurer que le joueur n'est actif qu'après la fin de l'intro
            newPlayer.SetActive(true);

            PurrLogger.Log($"[PlayerSpawner] Joueur {player} spawné après l'intro.");
        }
    }
}
