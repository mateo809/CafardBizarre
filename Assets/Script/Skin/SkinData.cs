using UnityEngine;

[CreateAssetMenu(fileName = "SkinData", menuName = "PurrNet/SkinData")]
public class SkinData : ScriptableObject
{
    [Header("Identification")]
    public string skinId;
    public string skinName;
    public Sprite previewIcon;

    [Header("3D Object")]
    public GameObject skinPrefab;

    [Header("Unlock")]
    public bool isUnlockedByDefault = false;
    public string unlockId; 

    [Header("Transform override")]
    public Vector3 positionOffset;
    public Vector3 rotationOffset;
    public Vector3 scaleOverride = Vector3.one;
}