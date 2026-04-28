using UnityEngine;
using UnityEngine.UI;
using PurrNet;
using TMPro;

/// <summary>
/// Affiche un prompt "[E] Monter" quand le joueur local est proche du véhicule.
/// Attacher sur le véhicule. Assigner un Canvas World Space avec un Text/TextMeshPro.
/// </summary>
public class VehicleProximityUI : MonoBehaviour
{
    [Header("UI")]
    public GameObject promptUI;         // Canvas ou GameObject "[E] Monter"
    public TextMeshProUGUI promptText;       // Optionnel : pour afficher "Véhicule plein"

    [Header("Détection")]
    public float showDistance = 4f;

    private VehicleController _vehicle;
    private Transform _localPlayer;

    private void Awake()
    {
        _vehicle = GetComponent<VehicleController>();
        if (promptUI != null) promptUI.SetActive(false);
    }

    private void Update()
    {

        // Trouve le joueur local (une seule fois)
        if (_localPlayer == null)
        {
            var all = FindObjectsByType<PlayerVehicleController>(FindObjectsSortMode.None);
            foreach (var p in all)
            {
                if (p.isOwner) { _localPlayer = p.transform; break; }
            }
            return;
        }

        float dist = Vector3.Distance(transform.position, _localPlayer.position);
        bool show = dist <= showDistance;

        if (promptUI != null && promptUI.activeSelf != show)
            promptUI.SetActive(show);

        if (show && promptText != null)
        {
            int freeSeat = _vehicle.GetFreeSeat();
            promptText.text = freeSeat == -1 ? "Véhicule plein" : "[E] Monter";
        }
    }
}