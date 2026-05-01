using UnityEngine;
using UnityEngine.UI;

public class SpeedLinesEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform lineContainer;
    [SerializeField] private Image linePrefab;

    [Header("Settings")]
    [SerializeField] private float spawnDistance = 150f; // Distance du centre
    [SerializeField] private float lifeTime = 0.5f;
    [SerializeField] private Vector2 speedRange = new Vector2(500f, 1000f);
    [SerializeField] private Vector2 lengthRange = new Vector2(100f, 300f);

    private void Update()
    {
        SpawnLineAt(Random.Range(0f, 360f));
    }
    // Appelle cette fonction depuis ton contrôleur de personnage ou véhicule
    public void SpawnLineAt(float angle)
    {
        if (lineContainer == null || linePrefab == null) return;

        Image img = Instantiate(linePrefab, lineContainer);
        RectTransform rt = img.rectTransform;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        // Positionnement en fonction de l'angle et de la distance
        Vector2 direction = new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad));
        rt.anchoredPosition = direction * spawnDistance;

        rt.localRotation = Quaternion.Euler(0f, 0f, -angle);
        rt.sizeDelta = new Vector2(2f, Random.Range(lengthRange.x, lengthRange.y));

        var mover = img.gameObject.AddComponent<SpeedLineMover>();
        mover.Init(direction, Random.Range(speedRange.x, speedRange.y), lifeTime);
    }
}

public class SpeedLineMover : MonoBehaviour
{
    private Vector2 direction;
    private float speed;
    private float lifeTime;
    private float age;
    private RectTransform rt;

    public void Init(Vector2 dir, float s, float lt)
    {
        direction = dir;
        speed = s;
        lifeTime = lt;
        rt = GetComponent<RectTransform>();
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        rt.anchoredPosition += direction * speed * Time.unscaledDeltaTime;

        float alpha = 1f - Mathf.Clamp01(age / lifeTime);
        Graphic g = GetComponent<Graphic>();
        if (g != null) g.color = new Color(1, 1, 1, alpha);

        if (age >= lifeTime) Destroy(gameObject);
    }
}