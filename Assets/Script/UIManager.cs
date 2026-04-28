using System.Collections;
using UnityEngine;

public class UIManager : MonoBehaviour
{

    public static UIManager Instance { get; private set; }

    [Header("Canvas (laisser vide = auto-détection au démarrage)")]
    [SerializeField] private Canvas _mainCanvas;

    [Header("Délai entre chaque tentative (secondes)")]
    [SerializeField] private float _retryInterval = 0.1f;

    [Header("Nombre maximum de tentatives (0.1 s chacune ? 50 = 5 s)")]
    [SerializeField] private int _maxRetries = 50;

    private bool _isReady = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (_mainCanvas == null)
        {
            StartCoroutine(FindCanvasWithRetry());
        }
        else
        {
            _isReady = true;
        }
    }

    private IEnumerator FindCanvasWithRetry()
    {
        int retryCount = 0;

        while (retryCount < _maxRetries)
        {
            Canvas canvas = FindCanvas();

            if (canvas != null)
            {
                _mainCanvas = canvas;
                _isReady = true;
                Debug.Log($"[UIManager] Canvas trouvé après {retryCount + 1} tentative(s) : '{canvas.gameObject.name}'");
                yield break;
            }

            retryCount++;
            yield return new WaitForSeconds(_retryInterval);
        }

        Debug.LogError("[UIManager] Canvas introuvable après 5 secondes ! " +
                       "Assignez-le manuellement dans l'Inspector ou taguez-le 'Canvas'.");
    }

    private static Canvas FindCanvas()
    {
        GameObject go = GameObject.FindWithTag("Canvas");
        if (go != null)
        {
            Canvas c = go.GetComponent<Canvas>();
            if (c != null && go.activeInHierarchy)
                return c;
        }

        foreach (Canvas c in FindObjectsOfType<Canvas>())
        {
            if (c.gameObject.activeInHierarchy)
                return c;
        }

        return null;
    }

    public Canvas MainCanvas => _mainCanvas;

    public bool IsReady => _isReady;

    public GameObject InstantiateInCanvas(GameObject prefab)
    {
        if (!_isReady || _mainCanvas == null)
        {
            Debug.LogWarning("[UIManager] InstantiateInCanvas appelé avant que le Canvas soit prêt.");
            return null;
        }

        return Instantiate(prefab, _mainCanvas.transform, false);
    }

    public T InstantiateInCanvas<T>(GameObject prefab) where T : Component
    {
        GameObject go = InstantiateInCanvas(prefab);
        return go != null ? go.GetComponentInChildren<T>() : null;
    }

    public IEnumerator WaitUntilReady(System.Action onReady)
    {
        if (_isReady)
        {
            onReady?.Invoke();
            yield break;
        }

        float elapsed = 0f;
        float timeout = _maxRetries * _retryInterval;

        while (!_isReady && elapsed < timeout)
        {
            elapsed += _retryInterval;
            yield return new WaitForSeconds(_retryInterval);
        }

        if (_isReady)
        {
            onReady?.Invoke();
        }
        else
        {
            Debug.LogError("[UIManager] WaitUntilReady : timeout atteint, UIManager jamais prêt.");
        }
    }
}