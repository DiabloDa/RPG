using UnityEngine;
using UnityEngine.UI;

public class PowerUpFeedbackUI : MonoBehaviour
{
    [SerializeField] private Color borderColor = new Color(1f, 0.9f, 0.15f, 0.95f);
    [SerializeField] private Color badgeColor = new Color(1f, 0.9f, 0.15f, 0.9f);
    [SerializeField, Min(1)] private int fadeLayers = 4;
    [SerializeField, Min(0.001f)] private float edgeThickness = 0.014f;
    [SerializeField, Min(0f)] private float edgeFadeStep = 0.009f;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private readonly System.Collections.Generic.List<Image> borderPieces = new System.Collections.Generic.List<Image>();
    private RectTransform badgeRoot;
    private Image badgeBackground;
    private Text badgeLabel;
    private Text badgeTimer;

    private bool isVisible;
    private float timerSeconds;

    private void Awake()
    {
        BuildUI();
        SetInvulnerabilityActive(false, 0f);
    }

    private void Update()
    {
        if (!isVisible)
        {
            return;
        }

        float pulse = 0.82f + 0.18f * Mathf.Sin(Time.unscaledTime * 8f);
        SetBorderAlpha(borderColor.a * pulse);
        badgeRoot.localScale = Vector3.one * (0.96f + 0.04f * Mathf.Sin(Time.unscaledTime * 9f));

        if (timerSeconds > 0f)
        {
            timerSeconds = Mathf.Max(0f, timerSeconds - Time.unscaledDeltaTime);
            if (badgeTimer != null)
            {
                badgeTimer.text = $"{timerSeconds:0.0}s";
            }
        }
    }

    public void SetInvulnerabilityActive(bool active, float remainingSeconds)
    {
        isVisible = active;
        timerSeconds = Mathf.Max(0f, remainingSeconds);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = active ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (badgeTimer != null)
        {
            badgeTimer.text = active ? $"{timerSeconds:0.0}s" : string.Empty;
        }

        if (badgeLabel != null)
        {
            badgeLabel.text = active ? "IMMUNE" : string.Empty;
        }

        SetBorderAlpha(active ? borderColor.a : 0f);
    }

    private void BuildUI()
    {
        GameObject canvasGo = new GameObject("PowerUpHUD");
        canvasGo.transform.SetParent(transform, false);

        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        canvasGroup = canvasGo.AddComponent<CanvasGroup>();

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGo.AddComponent<GraphicRaycaster>();

        CreateBorderEdge(canvasGo.transform, true, true);
        CreateBorderEdge(canvasGo.transform, true, false);
        CreateBorderEdge(canvasGo.transform, false, true);
        CreateBorderEdge(canvasGo.transform, false, false);

        badgeRoot = CreateBadge(canvasGo.transform);
    }

    private void CreateBorderEdge(Transform parent, bool horizontal, bool topOrLeft)
    {
        for (int i = 0; i < fadeLayers; i++)
        {
            float layerOffset = i * edgeFadeStep;
            float layerThickness = edgeThickness + layerOffset;
            float alpha = borderColor.a * (1f - (i / Mathf.Max(1f, (float)fadeLayers)));

            GameObject piece = new GameObject($"{(horizontal ? "H" : "V")}_{(topOrLeft ? "A" : "B")}_{i}");
            piece.transform.SetParent(parent, false);

            RectTransform rect = piece.AddComponent<RectTransform>();
            if (horizontal)
            {
                rect.anchorMin = topOrLeft ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
                rect.anchorMax = topOrLeft ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
                rect.offsetMin = new Vector2(0f, topOrLeft ? -layerThickness : 0f);
                rect.offsetMax = new Vector2(0f, topOrLeft ? 0f : layerThickness);
            }
            else
            {
                rect.anchorMin = topOrLeft ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
                rect.anchorMax = topOrLeft ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
                rect.offsetMin = new Vector2(topOrLeft ? 0f : -layerThickness, 0f);
                rect.offsetMax = new Vector2(topOrLeft ? layerThickness : 0f, 0f);
            }

            Image image = piece.AddComponent<Image>();
            image.color = new Color(borderColor.r, borderColor.g, borderColor.b, alpha);
            borderPieces.Add(image);
        }
    }

    private RectTransform CreateBadge(Transform parent)
    {
        GameObject root = new GameObject("InvulnerabilityBadge");
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0f);
        rootRect.anchorMax = new Vector2(0.5f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = new Vector2(0f, 44f);
        rootRect.sizeDelta = new Vector2(90f, 90f);

        badgeBackground = root.AddComponent<Image>();
        badgeBackground.color = new Color(badgeColor.r, badgeColor.g, badgeColor.b, 0.12f);

        GameObject icon = new GameObject("Icon");
        icon.transform.SetParent(root.transform, false);
        RectTransform iconRect = icon.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -10f);
        iconRect.sizeDelta = new Vector2(38f, 38f);

        Image iconImage = icon.AddComponent<Image>();
        iconImage.color = borderColor;

        Text iconText = CreateText("I", icon.transform, 16, TextAnchor.MiddleCenter, Color.black);
        RectTransform iconTextRect = iconText.GetComponent<RectTransform>();
        iconTextRect.anchorMin = Vector2.zero;
        iconTextRect.anchorMax = Vector2.one;
        iconTextRect.offsetMin = Vector2.zero;
        iconTextRect.offsetMax = Vector2.zero;

        badgeLabel = CreateText("IMMUNE", root.transform, 18, TextAnchor.MiddleCenter, borderColor);
        RectTransform labelRect = badgeLabel.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0f);
        labelRect.anchorMax = new Vector2(0.5f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 6f);
        labelRect.sizeDelta = new Vector2(120f, 22f);

        badgeTimer = CreateText(string.Empty, root.transform, 14, TextAnchor.UpperCenter, Color.white);
        RectTransform timerRect = badgeTimer.GetComponent<RectTransform>();
        timerRect.anchorMin = new Vector2(0.5f, 0f);
        timerRect.anchorMax = new Vector2(0.5f, 0f);
        timerRect.pivot = new Vector2(0.5f, 0f);
        timerRect.anchoredPosition = new Vector2(0f, -10f);
        timerRect.sizeDelta = new Vector2(60f, 18f);

        return rootRect;
    }

    private Text CreateText(string value, Transform parent, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent, false);

        Text text = go.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;

        RectTransform rect = text.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(100f, 24f);
        return text;
    }

    private void SetBorderAlpha(float alpha)
    {
        for (int i = 0; i < borderPieces.Count; i++)
        {
            if (borderPieces[i] == null)
            {
                continue;
            }

            float fade = 1f - (i % Mathf.Max(1, fadeLayers)) / Mathf.Max(1f, (float)fadeLayers);
            borderPieces[i].color = new Color(borderColor.r, borderColor.g, borderColor.b, alpha * Mathf.Clamp01(fade));
        }

        if (badgeBackground != null)
        {
            badgeBackground.color = new Color(badgeColor.r, badgeColor.g, badgeColor.b, isVisible ? 0.12f : 0f);
        }
    }
}
