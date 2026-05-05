using UnityEngine;
using PurrNet;

[RequireComponent(typeof(PlayerSkinAttachment))]
public class SkinApplierOnSpawn : NetworkBehaviour
{
    private const string SELECTED_SKIN_PREF = "SelectedSkinId";

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);

        if (!isOwner)
            return;

        string skinId = PlayerPrefs.GetString(SELECTED_SKIN_PREF, "");
        Debug.Log($"[SkinApplier] Loaded skinId = '{skinId}'");

        if (string.IsNullOrEmpty(skinId))
        {
            Debug.Log("[SkinApplier] Aucun skin sauvegardé.");
            return;
        }

        var attachment = GetComponent<PlayerSkinAttachment>();
        if (attachment == null)
        {
            Debug.LogWarning("[SkinApplier] PlayerSkinAttachment manquant.");
            return;
        }

        attachment.ApplySkin(skinId);
        Debug.Log($"[SkinApplier] Skin appliqué au spawn : {skinId}");
    }
}