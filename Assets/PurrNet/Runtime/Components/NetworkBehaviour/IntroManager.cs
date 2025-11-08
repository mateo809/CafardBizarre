using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class IntroManager : MonoBehaviour
{
    [Header("Caméra Intro")]
    [SerializeField] private Transform introCameraPivot;

    [Header("Points de caméra")]
    [SerializeField] private Transform cameraPoint1;
    [SerializeField] private Transform cameraPoint2;
    [SerializeField] private Transform cameraPoint3;

    [Header("UI Texte (GameObjects)")]
    [SerializeField] private GameObject cameraText1;
    [SerializeField] private GameObject cameraText2;
    [SerializeField] private GameObject cameraText3;

    [Header("Fade Noir")]
    [SerializeField] private Image blackFadeImage;
    [SerializeField] private float blackFadeDuration = 1f;

    [Header("Timing")]
    [SerializeField] private float cameraMoveToPointDuration = 3f;

    [Header("Layers à désactiver")]
    [SerializeField] private string[] layersToDisable = { "Player", "Enemy" };

    public GameObject Canvas;

    private bool introPlaying = false;
    private bool introSkipped = false;
    private List<GameObject> disabledObjects = new List<GameObject>();

    public static bool IntroFinished { get; private set; } = false;

    private void Start()
    {
        if (blackFadeImage != null)
            blackFadeImage.color = new Color(0, 0, 0, 1f); 

        DisableGameObjects();
        HideAllTexts();
        StartCoroutine(PlayIntro());

        Canvas.gameObject.SetActive(false);
    }

    private void DisableGameObjects()
    {
        disabledObjects.Clear();
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            if (!obj.activeInHierarchy) continue;

            string layerName = LayerMask.LayerToName(obj.layer);

            foreach (string layerToDisable in layersToDisable)
            {
                if (layerName == layerToDisable)
                {
                    disabledObjects.Add(obj);
                    obj.SetActive(false);
                    break;
                }
            }
        }

        Debug.Log($"[IntroManager] {disabledObjects.Count} objets désactivés (par layer)");
    }

    private void EnableGameObjects()
    {
        foreach (GameObject obj in disabledObjects)
            if (obj != null)
                obj.SetActive(true);

        disabledObjects.Clear();
        Debug.Log("[IntroManager] Tous les objets sont réactivés");
    }

    // === Gestion des textes par plan ===
    private void ShowText(GameObject textObject)
    {
        if (textObject != null) textObject.SetActive(true);
    }

    private void HideText(GameObject textObject)
    {
        if (textObject != null) textObject.SetActive(false);
    }

    private void HideAllTexts()
    {
        HideText(cameraText1);
        HideText(cameraText2);
        HideText(cameraText3);
    }

    private IEnumerator PlayIntro()
    {
        introPlaying = true;

        HideAllTexts();
        StartCoroutine(MoveCamera(cameraPoint1, cameraMoveToPointDuration));
        yield return StartCoroutine(FadeFromBlack()); 
        ShowText(cameraText1);                                                               
        yield return new WaitForSeconds(cameraMoveToPointDuration);
        HideText(cameraText1);
        yield return StartCoroutine(FadeToBlack());   

        HideAllTexts();
        StartCoroutine(MoveCamera(cameraPoint2, cameraMoveToPointDuration));
        yield return new WaitForSeconds(4);
        yield return StartCoroutine(FadeFromBlack());
        ShowText(cameraText2);
        yield return new WaitForSeconds(1);
        yield return new WaitForSeconds(cameraMoveToPointDuration);
        yield return StartCoroutine(FadeToBlack());
        HideText(cameraText2);

        HideAllTexts();
        StartCoroutine(MoveCamera(cameraPoint3, cameraMoveToPointDuration));
        yield return new WaitForSeconds(4);
        yield return StartCoroutine(FadeFromBlack());
        ShowText(cameraText3);
        yield return new WaitForSeconds(cameraMoveToPointDuration);
        HideText(cameraText3);
        yield return StartCoroutine(FadeToBlack());

        introPlaying = false;
        EndIntro();
    }

    private IEnumerator MoveCamera(Transform targetPoint, float duration)
    {
        if (targetPoint == null || introCameraPivot == null) yield break;

        Vector3 startPos = introCameraPivot.position;
        Quaternion startRot = introCameraPivot.rotation;

        Vector3 targetPos = targetPoint.position;
        Quaternion targetRot = targetPoint.rotation;

        float elapsed = 0f;
        while (elapsed < duration && !introSkipped)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            introCameraPivot.position = Vector3.Lerp(startPos, targetPos, t);
            introCameraPivot.rotation = Quaternion.Lerp(startRot, targetRot, t);

            yield return null;
        }

        introCameraPivot.position = targetPos;
        introCameraPivot.rotation = targetRot;
    }

    // === Fade Noir ===
    private IEnumerator FadeToBlack()
    {
        if (blackFadeImage == null) yield break;

        float elapsed = 0f;
        Color startColor = blackFadeImage.color;
        Color targetColor = new Color(0, 0, 0, 1f); 

        while (elapsed < blackFadeDuration && !introSkipped)
        {
            elapsed += Time.deltaTime;
            blackFadeImage.color = Color.Lerp(startColor, targetColor, elapsed / blackFadeDuration);
            yield return null;
        }

        blackFadeImage.color = targetColor; 
    }

    private IEnumerator FadeFromBlack()
    {
        if (blackFadeImage == null) yield break;

        float elapsed = 0f;
        Color startColor = blackFadeImage.color;
        Color targetColor = new Color(0, 0, 0, 0f); 

        while (elapsed < blackFadeDuration && !introSkipped)
        {
            elapsed += Time.deltaTime;
            blackFadeImage.color = Color.Lerp(startColor, targetColor, elapsed / blackFadeDuration);
            yield return null;
        }

        blackFadeImage.color = targetColor; 
    }

    private void EndIntro()
    {
        Canvas.gameObject.SetActive(true);
        IntroFinished = true;
        EnableGameObjects();
        HideAllTexts();
        if (introCameraPivot != null)
            introCameraPivot.gameObject.SetActive(false);

        Debug.Log("[IntroManager] Intro terminée - Jeu commencé!");
        blackFadeImage.gameObject.SetActive(false);
    }

    public bool IsIntroPlaying() => introPlaying;
}
