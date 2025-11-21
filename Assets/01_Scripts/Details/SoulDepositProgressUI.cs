using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI totalmente generada por código que muestra el progreso
/// de canalizar almas en el altar.
/// Es un singleton simple accesible vía GetOrCreateInstance().
/// </summary>
public class SoulDepositProgressUI : MonoBehaviour
{
    public static SoulDepositProgressUI Instance { get; private set; }

    private Canvas _canvas;
    private RectTransform _panel;
    private Image _background;
    private Image _progressFill;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _percentText;

    /// <summary>
    /// Crea (si no existe) o devuelve la instancia única del UI.
    /// </summary>
    public static SoulDepositProgressUI GetOrCreateInstance()
    {
        if (Instance != null)
            return Instance;

        var go = new GameObject("SoulDepositProgressUI");
        DontDestroyOnLoad(go);

        var ui = go.AddComponent<SoulDepositProgressUI>();
        return ui;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CreateUI();
        Hide();
    }

    /// <summary>
    /// Crea la jerarquía de Canvas + Panel + Barra + Textos por código.
    /// </summary>
    private void CreateUI()
    {
        // Canvas raíz
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 900; // alto para estar por encima del HUD normal

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        // Panel
        var panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(transform, false);
        _panel = panelGO.AddComponent<RectTransform>();
        _panel.sizeDelta = new Vector2(420f, 130f);
        _panel.anchorMin = new Vector2(0.5f, 0.18f);
        _panel.anchorMax = new Vector2(0.5f, 0.18f);
        _panel.pivot = new Vector2(0.5f, 0.5f);
        _panel.anchoredPosition = Vector2.zero;

        _background = panelGO.AddComponent<Image>();
        _background.color = new Color(0f, 0f, 0.02f, 0.78f);

        // Título
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(panelGO.transform, false);
        var titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -12f);
        titleRect.sizeDelta = new Vector2(380f, 32f);

        _titleText = titleGO.AddComponent<TextMeshProUGUI>();
        _titleText.alignment = TextAlignmentOptions.Center;
        _titleText.fontSize = 26f;
        _titleText.text = "Canalizando almas hacia el altar...";
        _titleText.color = new Color(0.85f, 0.9f, 1f, 1f);
        _titleText.enableWordWrapping = false;

        // Fondo de barra
        var barBgGO = new GameObject("ProgressBackground");
        barBgGO.transform.SetParent(panelGO.transform, false);
        var barBgRect = barBgGO.AddComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0.5f, 0.5f);
        barBgRect.anchorMax = new Vector2(0.5f, 0.5f);
        barBgRect.pivot = new Vector2(0.5f, 0.5f);
        barBgRect.anchoredPosition = new Vector2(0f, -5f);
        barBgRect.sizeDelta = new Vector2(380f, 26f);

        var barBgImage = barBgGO.AddComponent<Image>();
        barBgImage.color = new Color(0.15f, 0.15f, 0.3f, 0.9f);

        // Barra de progreso (fill)
        var barFillGO = new GameObject("ProgressFill");
        barFillGO.transform.SetParent(barBgGO.transform, false);
        var barFillRect = barFillGO.AddComponent<RectTransform>();
        barFillRect.anchorMin = new Vector2(0f, 0f);
        barFillRect.anchorMax = new Vector2(1f, 1f);
        barFillRect.pivot = new Vector2(0f, 0.5f);
        barFillRect.anchoredPosition = Vector2.zero;
        barFillRect.sizeDelta = Vector2.zero;

        _progressFill = barFillGO.AddComponent<Image>();
        _progressFill.color = new Color(0.35f, 0.8f, 1f, 1f);
        _progressFill.type = Image.Type.Filled;
        _progressFill.fillMethod = Image.FillMethod.Horizontal;
        _progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _progressFill.fillAmount = 0f;

        // Texto de porcentaje/tiempo
        var percentGO = new GameObject("PercentText");
        percentGO.transform.SetParent(panelGO.transform, false);
        var percentRect = percentGO.AddComponent<RectTransform>();
        percentRect.anchorMin = new Vector2(0.5f, 0f);
        percentRect.anchorMax = new Vector2(0.5f, 0f);
        percentRect.pivot = new Vector2(0.5f, 0f);
        percentRect.anchoredPosition = new Vector2(0f, 10f);
        percentRect.sizeDelta = new Vector2(380f, 32f);

        _percentText = percentGO.AddComponent<TextMeshProUGUI>();
        _percentText.alignment = TextAlignmentOptions.Center;
        _percentText.fontSize = 20f;
        _percentText.text = "0% - 0.0s";
        _percentText.color = new Color(0.8f, 0.95f, 1f, 1f);
        _percentText.enableWordWrapping = false;
    }

    /// <summary>
    /// Muestra/actualiza la barra con el progreso [0..1] y el tiempo restante.
    /// </summary>
    public void Show(float progress, float remainingSeconds)
    {
        if (_panel == null)
            return;

        progress = Mathf.Clamp01(progress);
        if (remainingSeconds < 0f)
            remainingSeconds = 0f;

        gameObject.SetActive(true);

        if (_progressFill != null)
        {
            _progressFill.fillAmount = progress;
        }

        if (_percentText != null)
        {
            float percent = progress * 100f;
            _percentText.text = $"{percent:0}% - {remainingSeconds:0.0}s";
        }
    }

    /// <summary>
    /// Oculta todo el panel.
    /// </summary>
    public void Hide()
    {
        if (_panel == null)
            return;

        gameObject.SetActive(false);
    }
}
