using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkinDatabase", menuName = "PurrNet/SkinDatabase")]
public class SkinDatabase : ScriptableObject
{
    [SerializeField] private List<SkinData> _skins = new();

    private Dictionary<string, SkinData> _lookup;

    private void OnEnable()
    {
        RebuildLookup();
    }

    private void RebuildLookup()
    {
        _lookup = new Dictionary<string, SkinData>();
        foreach (var skin in _skins)
        {
            if (skin == null || string.IsNullOrEmpty(skin.skinId)) continue;
            if (!_lookup.TryAdd(skin.skinId, skin))
                Debug.LogWarning($"[SkinDB] ID dupliqué : {skin.skinId}");
        }
    }

    public SkinData GetSkinById(string id)
    {
        if (_lookup == null) RebuildLookup();
        return _lookup.TryGetValue(id, out var s) ? s : null;
    }

    public IReadOnlyList<SkinData> GetAllSkins() => _skins;
}