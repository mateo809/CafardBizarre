// SkinUnlockManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkinUnlockManager : MonoBehaviour
{
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
    private BackendCaller _backendCaller;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        _backendCaller = GetComponent<BackendCaller>();
    }

    /// <summary>Charge les skins débloqués depuis le backend.</summary>
    public void LoadUnlockedFromBackend(BackendCaller caller, System.Action onDone = null)
    {
        Debug.Log("[SkinUnlock] LoadUnlockedFromBackend appelé");
        if (caller == null)
        {
            Debug.LogError("[SkinUnlock] caller est NULL !");
            return;
        }

        StartCoroutine(caller.GetUnlockedCosmetics(cosmetics =>
        {
            Debug.Log($"[SkinUnlock] Reçu {cosmetics?.Count} cosmetics du backend");
            _unlockedIds.Clear();
            foreach (var c in cosmetics)
            {
                Debug.Log($"[SkinUnlock] Ajout id: {c.id}");
                _unlockedIds.Add(c.id.ToString());
            }
            Debug.Log($"[SkinUnlock] _unlockedIds final: {string.Join(",", _unlockedIds)}");
            onDone?.Invoke();
        }));
    }

    /// <summary>Déverrouille un skin et l'envoie au backend.</summary>
    public void UnlockSkin(string unlockId)
    {
        if (string.IsNullOrEmpty(unlockId)) return;
        if (_unlockedIds.Contains(unlockId)) return;

        _unlockedIds.Add(unlockId);
        Debug.Log($"[SkinUnlock] Skin déverrouillé localement : {unlockId}");

        if (_backendCaller != null && int.TryParse(unlockId, out int id))
            StartCoroutine(_backendCaller.UnlockCosmetic(id));
        else
            Debug.LogWarning("[SkinUnlock] Impossible d'envoyer au backend : BackendCaller manquant ou unlockId non numérique.");
    }

    /// <summary>Vérifie si un skin est déverrouillé.</summary>
    public bool IsSkinUnlocked(SkinData skin)
    {
        if (skin == null) return false;
        return _unlockedIds.Contains(skin.unlockId);
    }


    /// <summary>Réinitialise tous les déverrouillages (debug).</summary>
    [ContextMenu("Reset All Unlocks")]
    public void ResetAllUnlocks()
    {
        _unlockedIds.Clear();
        Debug.Log("[SkinUnlock] Tous les déverrouillages réinitialisés.");
    }
}