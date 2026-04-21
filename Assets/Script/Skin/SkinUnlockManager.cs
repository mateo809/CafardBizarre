using System.Collections.Generic;
using Unity.Services.Relay.Models;
using UnityEngine;

/// <summary>
/// Gère le déverrouillage des skins via des IDs.
/// Les IDs déverrouillés sont persistés en PlayerPrefs.
/// </summary>
public class SkinUnlockManager : MonoBehaviour
{
    private const string PREFS_KEY = "UnlockedSkinIds";

    private static SkinUnlockManager _instance;
    public static SkinUnlockManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("SkinUnlockManager");
                _instance = go.AddComponent<SkinUnlockManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private HashSet<string> _unlockedIds = new();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        LoadUnlocked();
    }

    private void LoadUnlocked()
    {
        _unlockedIds.Clear();
        string raw = PlayerPrefs.GetString(PREFS_KEY, "");
        if (string.IsNullOrEmpty(raw)) return;
        foreach (var id in raw.Split(','))
            if (!string.IsNullOrEmpty(id))
                _unlockedIds.Add(id);
    }

    private void SaveUnlocked()
    {
        PlayerPrefs.SetString(PREFS_KEY, string.Join(",", _unlockedIds));
        PlayerPrefs.Save();
    }

    /// <summary>Déverrouille un skin par son unlockId.</summary>
    public void UnlockSkin(string unlockId)
    {
        if (string.IsNullOrEmpty(unlockId)) return;
        _unlockedIds.Add(unlockId);
        SaveUnlocked();
        Debug.Log($"[SkinUnlock] Skin déverrouillé : {unlockId}");
    }

    /// <summary>Vérifie si un skin est déverrouillé.</summary>
    public bool IsSkinUnlocked(SkinData skin)
    {
        if (skin == null) return false;
        if (skin.isUnlockedByDefault) return true;
        return _unlockedIds.Contains(skin.unlockId);
    }

    /// <summary>Réinitialise tous les déverrouillages (debug).</summary>
    [ContextMenu("Reset All Unlocks")]
    public void ResetAllUnlocks()
    {
        _unlockedIds.Clear();
        PlayerPrefs.DeleteKey(PREFS_KEY);
        Debug.Log("[SkinUnlock] Tous les déverrouillages réinitialisés.");
    }
}