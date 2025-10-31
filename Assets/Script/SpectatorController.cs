using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpectatorController : MonoBehaviour
{
    public Button nextButton;
    public Button prevButton;
    public TMP_Text targetText;

    private List<PlayerHealth> alivePlayers = new List<PlayerHealth>();
    private int currentIndex = 0;
    private PlayerHealth localPlayer;

    private Camera spectatorCam;

    void Start()
    {
        spectatorCam = Camera.main; 

        if (nextButton != null)
            nextButton.onClick.AddListener(NextPlayer);
        if (prevButton != null)
            prevButton.onClick.AddListener(PrevPlayer);

        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (prevButton != null) prevButton.gameObject.SetActive(false);
        if (targetText != null) targetText.gameObject.SetActive(false);
    }

    public void ActivateSpectator(PlayerHealth player)
    {
        localPlayer = player;
        RefreshPlayersList();

        Debug.Log($"Nombre de joueurs vivants : {alivePlayers.Count}");

        if (alivePlayers.Count == 0)
        {
            Debug.LogWarning("Aucun joueur vivant à spectate !");
            return;
        }

        // Commencer par le premier joueur de la liste
        currentIndex = 0;

        gameObject.SetActive(true);
        if (nextButton != null) nextButton.gameObject.SetActive(true);
        if (prevButton != null) prevButton.gameObject.SetActive(true);
        if (targetText != null) targetText.gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Mettre directement la caméra du spectateur sur la caméra du premier joueur
        UpdateCamera();
    }

    public void RefreshPlayersList()
    {
        alivePlayers.Clear();
        foreach (var p in PlayerHealth.AllPlayers)
        {
            if (p != localPlayer && p.currentHealth > 0)
                alivePlayers.Add(p);
        }
        if (currentIndex >= alivePlayers.Count)
            currentIndex = 0;
    }

    void LateUpdate()
    {
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        if (alivePlayers.Count == 0 || spectatorCam == null) return;

        Camera targetCam = alivePlayers[currentIndex].GetComponentInChildren<Camera>();
        if (targetCam != null)
        {
            // Position et rotation exactement comme la caméra du joueur
            spectatorCam.transform.position = targetCam.transform.position;
            spectatorCam.transform.rotation = targetCam.transform.rotation;

            if (targetText != null)
                targetText.text = alivePlayers[currentIndex].gameObject.name;
        }
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
}
