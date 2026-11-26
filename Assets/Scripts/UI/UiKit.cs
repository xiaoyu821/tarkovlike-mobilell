using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>内置字体。Android 上没有 Arial.ttf，只有 LegacyRuntime.ttf。</summary>
public static class UiFont
{
    private static Font _default;

    public static Font Default
    {
        get
        {
            if (_default != null) return _default;

            _default = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_default == null)
            {
                _default = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return _default;
        }
    }
}

/// <summary>保证场景里有 EventSystem，并把拖动阈值降到 1px。</summary>
public static class UiEventSystem
{
    public static void Ensure()
    {
        var existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        if (existing != null)
        {
            existing.pixelDragThreshold = 1;
            return;
        }

        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif

        var eventSystem = go.GetComponent<EventSystem>();
        if (eventSystem != null)
        {
            eventSystem.pixelDragThreshold = 1;
        }
    }
}

/// <summary>代码搭 uGUI 的小工具，避免每个界面重复写一堆 RectTransform 设置。</summary>
public static class UiKit
{
    public static Canvas CreateCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        UiEventSystem.Ensure();
        return canvas;
    }

    public static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = color.a > 0.01f;

        return rect;
    }

    public static Text Label(Transform parent, string name, string text, int fontSize, TextAnchor anchor,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        var label = go.GetComponent<Text>();
        label.text = text;
        label.font = UiFont.Default;
        label.fontSize = fontSize;
        label.alignment = anchor;
        label.color = color;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;

        return label;
    }

    public static Button TextButton(Transform parent, string name, string text, Vector2 anchor,
        Vector2 position, Vector2 size, Color background, Color textColor, int fontSize, UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var image = go.GetComponent<Image>();
        image.color = background;
        image.raycastTarget = true;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        var label = Label(rect, "Label", text, fontSize, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, textColor);

        // 按钮按下时给一点视觉反馈
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;

        label.raycastTarget = false;
        return button;
    }

    public static void Clear(Transform parent)
    {
        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            Object.Destroy(parent.GetChild(i).gameObject);
        }
    }
}