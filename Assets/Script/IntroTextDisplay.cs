using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;

public class IntroTextDisplay : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Timing")]
    [SerializeField] private float fadeInDuration = 1f;
    [SerializeField] private float fadeOutDuration = 1f;
    [SerializeField] private float displayDuration = 4f;


    public IEnumerator DisplayText(string title, string description)
    {
        if (titleText) titleText.text = title;
        if (descriptionText) descriptionText.text = description;

        yield return FadeCanvas(0f, 1f, fadeInDuration); // fade in
        yield return new WaitForSeconds(displayDuration); // temps visible
        yield return FadeCanvas(1f, 0f, fadeOutDuration); // fade out
    }

    private IEnumerator FadeCanvas(float from, float to, float duration)
    {
        if (!canvasGroup) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    public void HideInstant()
    {
        if (canvasGroup)
            canvasGroup.alpha = 0f;
    }
}
