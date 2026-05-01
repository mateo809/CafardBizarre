using System.Collections.Generic;
using UnityEngine;

public enum AudioType
{
    Step,
    Hit,
    PickItem,
    DropItem,
    mobStep,
    Die,
    HeartBeat,
    SellItem,
    Vacuum,
    Fly,
    Car,
}

public enum AudioSourceType
{
    Game,
    Player,
    Mob,
}

public class AudioController : MonoBehaviour
{
    public static AudioController Instance;

    [Range(0f, 50f)]
    public float volume = 50f;

    public AudioSource gameSource;
    public AudioSource playerSource;
    public AudioSource mobSource;

    [System.Serializable]
    public struct AudioData
    {
        public AudioClip clip;
        public AudioType type;
    }

    public AudioData[] audioDatas;

    private readonly Dictionary<AudioType, AudioSource> _playingSources = new Dictionary<AudioType, AudioSource>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        ApplyVolume();
    }

    public void ApplyVolume()
    {
        if (gameSource != null) gameSource.volume = volume;
        if (playerSource != null) playerSource.volume = volume;
        if (mobSource != null) mobSource.volume = volume;
    }

    public void PlaySound(AudioType type, AudioSourceType sourceType, Vector3 pos = default)
    {
        AudioClip clip = GetClip(type);
        if (clip == null) return;

        switch (sourceType)
        {
            case AudioSourceType.Game:
                if (gameSource == null) { Debug.LogError("gameSource non assigné !"); return; }
                gameSource.PlayOneShot(clip);
                break;

            case AudioSourceType.Player:
                if (pos != default)
                    AudioSource.PlayClipAtPoint(clip, pos, volume);
                else
                {
                    if (playerSource == null) { Debug.LogError("playerSource non assigné !"); return; }
                    playerSource.PlayOneShot(clip);
                }
                break;

            case AudioSourceType.Mob:
                if (mobSource == null) { Debug.LogError("mobSource non assigné !"); return; }
                mobSource.PlayOneShot(clip);
                break;
        }
    }

    public void PlayLoopedSound(AudioType type, AudioSourceType sourceType)
    {
        AudioClip clip = GetClip(type);
        if (clip == null) return;

        if (_playingSources.ContainsKey(type)) return;

        GameObject go = new GameObject("Audio_" + type);
        go.transform.parent = transform;

        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.volume = volume;
        source.Play();

        _playingSources[type] = source;
    }

    public void StopSound(AudioType type)
    {
        if (!_playingSources.TryGetValue(type, out AudioSource source)) return;

        source.Stop();
        Destroy(source.gameObject);
        _playingSources.Remove(type);
    }

    private AudioSource GetSource(AudioSourceType sourceType)
    {
        switch (sourceType)
        {
            case AudioSourceType.Game:
                if (gameSource == null) Debug.LogError("gameSource non assigné !");
                return gameSource;

            case AudioSourceType.Player:
                if (playerSource == null) Debug.LogError("playerSource non assigné !");
                return playerSource;

            case AudioSourceType.Mob:
                if (mobSource == null) Debug.LogError("mobSource non assigné !");
                return mobSource;
        }

        return null;
    }

    private AudioClip GetClip(AudioType type)
    {
        foreach (AudioData data in audioDatas)
        {
            if (data.type == type)
            {
                if (data.clip == null)
                {
                    Debug.LogError($"Le clip pour le type {type} est assigné mais vide dans l'Inspector !");
                    return null;
                }

                return data.clip;
            }
        }

        Debug.LogError($"Aucun clip trouvé pour le type : {type}. Vérifie ton tableau audioDatas dans l'Inspector.");
        return null;
    }

#if UNITY_EDITOR
    [ContextMenu("Test : jouer Step")]
    private void TestPlayStep() => PlaySound(AudioType.Step, AudioSourceType.Player);
#endif
}