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

}
public enum AudioSourceType
{
    Game,
    Player,
    Mob,
}



public class AudioController : MonoBehaviour
{


    static public AudioController Instance;


    public float volume = 1f;

    public AudioSource gameSource;
    public AudioSource playerSource;
    public AudioSource mobSource;

    [System.Serializable] public struct AudioData 
    {
        public AudioClip clip; 
        public AudioType type;
            }

    public AudioData[] audioDatas;


    private void Awake()
    {
        Instance = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameSource.volume = volume;
        playerSource.volume = volume;
        mobSource.volume = volume;
    }

    // Update is called once per frame
    public void PlaySound(AudioType type, AudioSourceType sourceType)
    {
        AudioClip clip = getClip(type);

        if (sourceType == AudioSourceType.Game)
        {
            gameSource.PlayOneShot(clip);
        }
        else if (sourceType == AudioSourceType.Player)
        { 
            playerSource.PlayOneShot(clip);

        }
        else if (sourceType == AudioSourceType.Mob)
        { 
           mobSource.PlayOneShot(clip);

        }
    }

    AudioClip getClip(AudioType type)
    {
        foreach (AudioData data in audioDatas)
        {
            if (data.type == type)
            {
                return data.clip;
            }

        }
        Debug.LogError("No clip found for type " + type);
        return null;
    }
}
