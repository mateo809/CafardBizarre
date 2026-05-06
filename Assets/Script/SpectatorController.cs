using System.Collections;
using System.Collections.Generic;
using TMPro;
using PurrNet;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SpectatorController : NetworkBehaviour
{
    [Header("UI")]
    public Button nextButton;
    public Button prevButton;
    public TMP_Text targetText;

    [Header("Camera")]
    public Camera spectatorCam;
    public float cameraHeight = 1.5f;
    public float cameraDistance = 3f;

    [Header("Game Over")]
    [PurrScene, SerializeField] private string _looseScene;

    private List<PlayerHealth> alivePlayers = new List<PlayerHealth>();
    private int currentIndex = 0;
    private PlayerHealth localPlayer;
    private bool isSpectating = false;
    private bool _gameEnded = false;

    void Start()
    {
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (prevButton != null) prevButton.gameObject.SetActive(false);
        if (targetText != null) targetText.gameObject.SetActive(false);

        if (nextButton != null) nextButton.onClick.AddListener(NextPlayer);
        if (prevButton != null) prevButton.onClick.AddListener(PrevPlayer);
        if (spectatorCam != null)
            spectatorCam.gameObject.SetActive(false);
    }

    public void ActivateSpectator(PlayerHealth player)
    {
        if (!player.isOwner) return;

        localPlayer = player;
        StartCoroutine(WaitAndInit());
    }

    private IEnumerator WaitAndInit()
    {
        yield return new WaitForSeconds(0.25f);

        RefreshPlayersList();

        if (alivePlayers.Count == 0)
        {
            Debug.LogWarning("[Spectator] Aucun joueur vivant trouvé ! Jeu terminé.");
            // Loose immédiatement s'il n'y a pas d'autres joueurs
            if (isServer)
            {
                TriggerGameOverRPC(false);
            }
            yield break;
        }

        isSpectating = true;
        currentIndex = 0;

        if (spectatorCam != null) spectatorCam.gameObject.SetActive(true);
        if (nextButton != null) nextButton.gameObject.SetActive(true);
        if (prevButton != null) prevButton.gameObject.SetActive(true);
        if (targetText != null) targetText.gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        UpdateCamera();
    }

    public void RefreshPlayersList()
    {
        alivePlayers.Clear();

        foreach (var p in PlayerHealth.AllPlayers)
        {
            if (p != null && p != localPlayer && p.currentHealth > 0)
                alivePlayers.Add(p);
        }

        Debug.Log($"[Spectator] Joueurs vivants : {alivePlayers.Count}");
    }

    void LateUpdate()
    {
        if (!isSpectating || _gameEnded)
            return;

        // Vérifier si tous les autres joueurs sont morts
        if (isServer)
        {
            RefreshPlayersList();

            if (alivePlayers.Count == 0)
            {
                Debug.Log("[Spectator] Plus de joueurs vivants ! Jeu terminé.");
                _gameEnded = true;
                TriggerGameOverRPC(false);
                return;
            }
        }

        UpdateCamera();
    }

    private void UpdateCamera()
    {
        if (alivePlayers.Count == 0 || spectatorCam == null)
            return;

        PlayerHealth target = alivePlayers[currentIndex];
        if (target == null)
            return;

        Transform t = target.transform;
        Vector3 offset = -t.forward * cameraDistance + Vector3.up * cameraHeight;
        spectatorCam.transform.position = t.position + offset;
        spectatorCam.transform.LookAt(t.position + Vector3.up * cameraHeight);

        if (targetText != null)
            targetText.text = $"Spectating: {target.gameObject.name}";
    }

    public void NextPlayer()
    {
        if (alivePlayers.Count == 0) return;

        currentIndex = (currentIndex + 1) % alivePlayers.Count;
        UpdateCamera();
    }

    public void PrevPlayer()
    {
        if (alivePlayers.Count == 0) return;

        currentIndex = (currentIndex - 1 + alivePlayers.Count) % alivePlayers.Count;
        UpdateCamera();
    }

    [Header("Backend")]
    [SerializeField] private BackendCaller _backendCaller;

    [ObserversRpc]
    private void TriggerGameOverRPC(bool isWin)
    {
        _gameEnded = true;

        if (isWin)
        {
            Debug.Log("[Spectator] Win!");

            if (_backendCaller != null && GlobalEconomyManager._instance != null)
            {
                int remainingTime = Mathf.RoundToInt(GlobalEconomyManager._instance.GetRemainingTime());
                int totalMoney = GlobalEconomyManager._instance.GetTotalMoney();

                StartCoroutine(_backendCaller.UnlockRandomCosmetic(cosmetic =>
                {
                    if (cosmetic != null)
                        Debug.Log($"Nouveau chapeau débloqué : {cosmetic.name} !");
                    else
                        Debug.Log("Tous les cosmetics sont déjà débloqués !");
                }));

                StartCoroutine(_backendCaller.UpdateHighscore(remainingTime, totalMoney));
            }
        }
        else
        {
            Debug.Log("[Spectator] Loose!");
            SceneManager.LoadSceneAsync(_looseScene);
        }
    }
}