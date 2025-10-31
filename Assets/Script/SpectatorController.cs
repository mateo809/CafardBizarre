using System.Collections.Generic;
using TMPro;
using PurrNet;
using UnityEngine;
using UnityEngine.UI;

public class SpectatorController : NetworkBehaviour
{
    [Header("UI")]
    public Button nextButton;
    public Button prevButton;
    public TMP_Text targetText;

    [Header("Camera")]
    public Camera spectatorCam; // assignée dans l'Inspector, désactivée par défaut

    private List<PlayerHealth> alivePlayers = new List<PlayerHealth>();
    private int currentIndex = 0;
    private PlayerHealth localPlayer;

    void Start()
    {
        // Assure-toi que les boutons sont désactivés au départ
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (prevButton != null) prevButton.gameObject.SetActive(false);
        if (targetText != null) targetText.gameObject.SetActive(false);

        // Lier les boutons aux fonctions
        if (nextButton != null) nextButton.onClick.AddListener(NextPlayer);
        if (prevButton != null) prevButton.onClick.AddListener(PrevPlayer);

        // La caméra de spectateur doit être inactive par défaut
        if (spectatorCam != null) spectatorCam.gameObject.SetActive(false);
    }

    // Appelée quand le joueur local meurt
    public void ActivateSpectator(PlayerHealth player)
    {
        if (!player.isOwner) return; // uniquement pour le joueur local

        localPlayer = player;
        RefreshPlayersList();

        Debug.Log($"Nombre de joueurs vivants : {alivePlayers.Count}");

        if (alivePlayers.Count == 0)
        {
            Debug.LogWarning("Aucun joueur vivant à spectate !");
            return;
        }

        currentIndex = 0;

        // Active la caméra de spectateur
        if (spectatorCam != null)
            spectatorCam.gameObject.SetActive(true);

        // Active l'UI
        if (nextButton != null) nextButton.gameObject.SetActive(true);
        if (prevButton != null) prevButton.gameObject.SetActive(true);
        if (targetText != null) targetText.gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Position initiale sur le premier joueur
        UpdateCamera();
    }

    // Met à jour la liste des joueurs vivants
    public void RefreshPlayersList()
    {
        alivePlayers.Clear();
        Debug.Log("=== RefreshPlayersList ===");
        foreach (var p in PlayerHealth.AllPlayers)
        {
            Debug.Log($"{p.gameObject.name} - Health: {p.currentHealth}");
            if (p != localPlayer && p.currentHealth > 0)
                alivePlayers.Add(p);
        }
        Debug.Log($"Nombre de joueurs vivants après filtrage: {alivePlayers.Count}");

        if (currentIndex >= alivePlayers.Count)
            currentIndex = 0;
    }

    void LateUpdate()
    {
        UpdateCamera();
    }

    // Copie la position et rotation de la caméra du joueur ciblé
    private void UpdateCamera()
    {
        if (alivePlayers.Count == 0 || spectatorCam == null) return;

        Camera targetCam = alivePlayers[currentIndex].GetComponentInChildren<Camera>();
        if (targetCam != null)
        {
            spectatorCam.transform.position = targetCam.transform.position;
            spectatorCam.transform.rotation = targetCam.transform.rotation;

            if (targetText != null)
                targetText.text = alivePlayers[currentIndex].gameObject.name;
        }
    }

    // Passer au joueur suivant
    public void NextPlayer()
    {
        if (alivePlayers.Count == 0) return;
        currentIndex = (currentIndex + 1) % alivePlayers.Count;
        UpdateCamera();
    }

    // Passer au joueur précédent
    public void PrevPlayer()
    {
        if (alivePlayers.Count == 0) return;
        currentIndex = (currentIndex - 1 + alivePlayers.Count) % alivePlayers.Count;
        UpdateCamera();
    }
}
