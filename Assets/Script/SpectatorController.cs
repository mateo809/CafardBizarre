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
    public Camera spectatorCam;
    public float cameraHeight = 1.5f; 
    public float cameraDistance = 0f; 

    private List<PlayerHealth> alivePlayers = new List<PlayerHealth>();
    private int currentIndex = 0;
    private PlayerHealth localPlayer;

    void Start()
    {
        // Désactive l'UI au départ
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (prevButton != null) prevButton.gameObject.SetActive(false);
        if (targetText != null) targetText.gameObject.SetActive(false);

        // Lien boutons
        if (nextButton != null) nextButton.onClick.AddListener(NextPlayer);
        if (prevButton != null) prevButton.onClick.AddListener(PrevPlayer);

        // Caméra inactive par défaut
        if (spectatorCam != null)
            spectatorCam.gameObject.SetActive(false);
    }

    // Appelée quand le joueur local meurt
    public void ActivateSpectator(PlayerHealth player)
    {
        if (!player.isOwner) return; // seulement pour le joueur local

        localPlayer = player;
        RefreshPlayersList();

        if (alivePlayers.Count == 0)
        {
            Debug.LogWarning("Aucun joueur vivant à spectate !");
            return;
        }

        currentIndex = 0;

        // Active caméra et UI
        if (spectatorCam != null) spectatorCam.gameObject.SetActive(true);
        if (nextButton != null) nextButton.gameObject.SetActive(true);
        if (prevButton != null) prevButton.gameObject.SetActive(true);
        if (targetText != null) targetText.gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        UpdateCamera();
    }

    // Met à jour la liste des joueurs vivants
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

        Transform target = alivePlayers[currentIndex].transform;
        Vector3 camPosition = target.position + Vector3.up * cameraHeight - target.forward * cameraDistance;

        spectatorCam.transform.position = camPosition;
        spectatorCam.transform.rotation = Quaternion.LookRotation(target.position + Vector3.up * cameraHeight - camPosition);

        if (targetText != null)
            targetText.text = alivePlayers[currentIndex].gameObject.name;
    }

    // Joueur suivant
    public void NextPlayer()
    {
        if (alivePlayers.Count == 0) return;
        currentIndex = (currentIndex + 1) % alivePlayers.Count;
        UpdateCamera();
    }

    // Joueur précédent
    public void PrevPlayer()
    {
        if (alivePlayers.Count == 0) return;
        currentIndex = (currentIndex - 1 + alivePlayers.Count) % alivePlayers.Count;
        UpdateCamera();
    }
}
