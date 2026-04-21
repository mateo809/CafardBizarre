using UnityEngine;
using PurrNet;

/// <summary>
/// À placer sur le prefab joueur aux côtés de PlayerSkinAttachment.
/// Lit le skin sauvegardé en PlayerPrefs et l'applique automatiquement
/// quand le joueur est spawné et qu'il en est le owner.
/// </summary>
[RequireComponent(typeof(PlayerSkinAttachment))]
public class SkinApplierOnSpawn : NetworkBehaviour
{
    private const string SELECTED_SKIN_PREF = "SelectedSkinId";

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        if (!isOwner) return;

        string skinId = PlayerPrefs.GetString(SELECTED_SKIN_PREF, "");
        if (string.IsNullOrEmpty(skinId))
        {
            Debug.Log("[SkinApplier] Aucun skin sauvegardé.");
            return;
        }

        var attachment = GetComponent<PlayerSkinAttachment>();
        attachment.ApplySkin(skinId);
        Debug.Log($"[SkinApplier] Skin appliqué au spawn : {skinId}");
    }
}