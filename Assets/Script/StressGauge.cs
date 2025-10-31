using PurrNet;
using UnityEngine;

public class StressGauge : NetworkBehaviour
{
    [Header("Références")]
    public RectTransform needle;
    public float minAngle = -90f;
    public float maxAngle = 90f;
    public float rotationSpeed = 180f;

    [Header("PlayerStress")]
    public PlayerStress playerStress;

    public void SetPlayerStress(PlayerStress stress)
    {
        playerStress = stress;
    }

    void Update()
    {
        if (needle == null || playerStress == null) return;

        float targetAngle = Mathf.Lerp(minAngle, maxAngle, playerStress.currentStress / 100f);

        needle.localRotation = Quaternion.RotateTowards(
            needle.localRotation,
            Quaternion.Euler(0, 0, targetAngle),
            rotationSpeed * Time.deltaTime
        );
    }
}
